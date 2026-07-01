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
    }
}
