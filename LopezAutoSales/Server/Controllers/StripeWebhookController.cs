using LopezAutoSales.Server.Configuration;
using LopezAutoSales.Server.Models;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Disambiguate our domain entities from same-named Stripe SDK types.
using Account = LopezAutoSales.Shared.Models.Account;
using Payment = LopezAutoSales.Shared.Models.Payment;

namespace LopezAutoSales.Server.Controllers
{
    // Stripe webhook. Unauthenticated by design — the Stripe signature IS the auth.
    // Absolute route (like SitemapController), so it bypasses the api/[controller] prefix.
    [ApiController]
    public class StripeWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StripeWebhookController> _logger;
        private readonly StripeOptions _opts;

        public StripeWebhookController(ApplicationDbContext context,
            ILogger<StripeWebhookController> logger, IOptions<StripeOptions> opts)
        {
            _context = context;
            _logger = logger;
            _opts = opts.Value;
        }

        [HttpPost("/api/stripe/webhook")]
        [RequestSizeLimit(65_536)] // Stripe events are tiny; cap the body on this unauthenticated endpoint.
        public async Task<IActionResult> Handle()
        {
            if (string.IsNullOrWhiteSpace(_opts.WebhookSecret))
            {
                _logger.LogWarning("Stripe webhook received but WebhookSecret is not configured.");
                return BadRequest();
            }

            string json = await new StreamReader(Request.Body).ReadToEndAsync(); // RAW body — no [FromBody]
            Event e;
            try
            {
                // Verifies the HMAC signature + 5-minute replay tolerance; throws on mismatch.
                e = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], _opts.WebhookSecret);
            }
            catch (StripeException)
            {
                return BadRequest(); // bad/missing signature
            }

            switch (e.Type)
            {
                case "payment_intent.succeeded": // SINGLE source of truth (card fast, ACH on settle)
                    await RecordPaymentIdempotentAsync(e.Data.Object as PaymentIntent);
                    break;
                case "payment_intent.payment_failed":
                    // MVP records only on settled success, so there's no recorded Payment to reverse.
                    _logger.LogWarning("Stripe payment failed: {Id}", (e.Data.Object as PaymentIntent)?.Id);
                    break;
                case "checkout.session.async_payment_failed": // ACH bounce — Data.Object is a Session
                    _logger.LogWarning("Stripe ACH payment failed for session {Id}",
                        (e.Data.Object as Stripe.Checkout.Session)?.Id);
                    break;
                case "charge.refunded": // dashboard refund, full or partial (AmountRefunded is cumulative)
                    await ApplyRefundAsync(e.Data.Object as Charge);
                    break;
                case "charge.dispute.funds_withdrawn": // ACH/card dispute — money actually left
                    await ApplyDisputeAsync(e.Data.Object as Dispute, withdrawn: true);
                    break;
                case "charge.dispute.funds_reinstated": // dispute won — money came back
                    await ApplyDisputeAsync(e.Data.Object as Dispute, withdrawn: false);
                    break;
            }
            return Ok(); // 2xx so Stripe stops retrying — even for events we ignore/dedupe.
        }

        private async Task RecordPaymentIdempotentAsync(PaymentIntent pi)
        {
            if (pi == null || string.IsNullOrEmpty(pi.Id))
                return;
            if (pi.Metadata == null || !pi.Metadata.TryGetValue("accountId", out string accStr)
                || !int.TryParse(accStr, out int accountId))
            {
                _logger.LogWarning("Stripe pi {Id} missing/invalid accountId metadata", pi.Id);
                return;
            }
            if (await _context.Payments.AnyAsync(p => p.StripePaymentIntentId == pi.Id))
                return; // idempotent (duplicate / out-of-order delivery)

            Account account = await _context.Accounts.Include(a => a.Payments)
                .Include(a => a.Sale).ThenInclude(s => s.Car).FirstOrDefaultAsync(a => a.Id == accountId);
            if (account == null)
            {
                _logger.LogWarning("Stripe pi {Id} account {Acc} missing", pi.Id, accountId);
                return;
            }

            decimal amount = pi.AmountReceived / 100m; // trust Stripe's settled amount, not the client
            // The money has already settled, so the payment is always recorded — but flag the
            // cases that need a human (refund/review): the /pay page blocks repossessed accounts
            // and clamps to the balance, yet an ACH debit settles days after checkout, so the
            // account state can change in between.
            decimal balanceBefore = account.Balance();
            string flag = "";
            if (account.IsRepossessed)
                flag += " [REPOSSESSED — review/refund]";
            if (amount > balanceBefore)
                flag += $" [EXCEEDS BALANCE {balanceBefore:C} — review/refund]";
            if (flag.Length > 0)
                _logger.LogWarning("Stripe pi {Id} for account {Acc} needs review:{Flag}", pi.Id, accountId, flag);
            Payment payment = new Payment
            {
                AccountId = accountId,
                Amount = amount,
                Date = DateTime.Now, // local — EnableLegacyTimestampBehavior maps to timestamp w/o tz
                StripePaymentIntentId = pi.Id
            };
            // Balance() BEFORE adding the payment — mirrors AccountController.AddPayment.
            account.IsPaid = balanceBefore <= amount;

            // Inline audit (don't reuse AccountController.Audit — it reads User.Identity; none here).
            _context.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.Now,
                User = "Stripe (online)",
                Action = "OnlinePaymentRecorded",
                Details = $"{account.Sale.Buyers()} [{account.Sale.Car.Name()}] {amount:C} via {pi.Id}{flag}"
            });
            _context.Payments.Add(payment);
            _logger.LogInformation("Online payment {Amt:C} recorded for account {Acc} ({Pi})", amount, accountId, pi.Id);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505")
            {
                // The unique filtered index caught a race — already recorded, treat as success.
                // Any OTHER DbUpdateException (e.g. a transient DB outage) propagates -> 500 ->
                // Stripe retries, so a settled payment is never silently dropped.
                _logger.LogInformation("Stripe pi {Id} already recorded (unique index race).", pi.Id);
            }
        }

        // A dashboard refund reduces the recorded Payment to the charge's still-settled
        // remainder (Amount - AmountRefunded). Computing an absolute value keeps this
        // idempotent under duplicate event delivery, including repeated partial refunds.
        private async Task ApplyRefundAsync(Charge charge)
        {
            if (charge?.PaymentIntentId == null)
                return;
            Payment payment = await FindOnlinePaymentAsync(charge.PaymentIntentId);
            if (payment == null)
                return;
            decimal newAmount = Math.Max(0, (charge.Amount - charge.AmountRefunded) / 100m);
            AdjustPayment(payment, newAmount,
                $"refund of {charge.AmountRefunded / 100m:C} via {charge.PaymentIntentId}");
            await _context.SaveChangesAsync();
        }

        // Disputes are relative adjustments (Stripe fires funds_withdrawn once per dispute,
        // funds_reinstated once if it's won); each adjustment is audit-logged so a human
        // can reconcile if anything ever double-fires.
        private async Task ApplyDisputeAsync(Dispute dispute, bool withdrawn)
        {
            if (dispute?.PaymentIntentId == null)
                return;
            Payment payment = await FindOnlinePaymentAsync(dispute.PaymentIntentId);
            if (payment == null)
                return;
            decimal delta = dispute.Amount / 100m;
            decimal newAmount = Math.Max(0, payment.Amount + (withdrawn ? -delta : delta));
            AdjustPayment(payment, newAmount,
                $"dispute {(withdrawn ? "withdrew" : "reinstated")} {delta:C} via {dispute.PaymentIntentId}");
            await _context.SaveChangesAsync();
        }

        private async Task<Payment> FindOnlinePaymentAsync(string paymentIntentId)
        {
            Payment payment = await _context.Payments
                .Include(p => p.Account).ThenInclude(a => a.Payments)
                .Include(p => p.Account).ThenInclude(a => a.Sale).ThenInclude(s => s.Car)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
            if (payment == null)
                // e.g. refund of a charge we never recorded (failed/duplicate pi) — nothing to reverse.
                _logger.LogWarning("Stripe refund/dispute for pi {Id}: no recorded payment found.", paymentIntentId);
            return payment;
        }

        private void AdjustPayment(Payment payment, decimal newAmount, string reason)
        {
            if (payment.Amount == newAmount)
                return; // duplicate delivery — don't pollute the audit trail with no-ops
            Account account = payment.Account;
            _logger.LogWarning("Online payment adjusted for account {Acc}: {Old:C} -> {New:C} ({Reason})",
                account.Id, payment.Amount, newAmount, reason);
            _context.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.Now,
                User = "Stripe (online)",
                Action = "OnlinePaymentAdjusted",
                Details = $"{account.Sale.Buyers()} [{account.Sale.Car.Name()}] {payment.Amount:C} -> {newAmount:C}: {reason}"
            });
            payment.Amount = newAmount;
            // Balance() reflects the mutated tracked payment; reopen the account if the
            // reversal reintroduced a balance, or close it if a reinstatement finished it.
            account.IsPaid = account.Balance() <= 0;
        }
    }
}
