using LopezAutoSales.Server.Services;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Pages
{
    // Public, unauthenticated "Make a Payment" page. Customers find their account by a
    // low-sensitivity two-factor lookup (last name + last 4 of the car VIN) and are
    // redirected to Stripe-hosted Checkout. No card/bank data ever touches our server.
    [EnableRateLimiting("pay-lookup")]
    public class PayModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IStripeCheckoutService _checkout;
        private readonly ILogger<PayModel> _logger;
        private readonly ITimeLimitedDataProtector _protector;

        public PayModel(ApplicationDbContext context, IStripeCheckoutService checkout,
            IDataProtectionProvider dataProtection, ILogger<PayModel> logger)
        {
            _context = context;
            _checkout = checkout;
            _logger = logger;
            _protector = dataProtection.CreateProtector("Pay.Lookup").ToTimeLimitedDataProtector();
        }

        [BindProperty] public string LastName { get; set; }
        [BindProperty] public string VinLast4 { get; set; }
        [BindProperty] public decimal? Amount { get; set; }   // optional override, server-clamped
        [BindProperty] public string Token { get; set; }       // hidden; carries protected accountId

        public string Message { get; set; }                    // uniform feedback (no oracle)
        public bool Found { get; set; }
        public string MaskedBuyer { get; set; }                // "J. ••••" first-initial only
        public string CarLabel { get; set; }                   // "2015 Ford Focus"
        public string VinMask { get; set; }                    // "VIN •••• 1234"
        public decimal AmountDue { get; set; }                 // LateDue() clamped to Balance()
        public decimal RemainingBalance { get; set; }          // full payoff — shown so customers can pay it down knowingly

        // Surface whether online payments are wired up so the page can degrade gracefully.
        public bool PaymentsAvailable => _checkout.IsConfigured;

        public IActionResult OnGet() => Page();

        public IActionResult OnPostLookup()
        {
            string last4 = (VinLast4 ?? "").Trim().ToUpper();
            string name = (LastName ?? "").Trim().ToLowerInvariant();
            // Uniform response set unconditionally — identical render on miss vs. found.
            Message = "If we found a matching account, the payment form will appear below. " +
                "If it doesn't, double-check your last name and the last 4 of your VIN.";
            if (last4.Length != 4 || name.Length == 0)
            {
                Found = false;
                return Page();
            }

            // VIN EndsWith is SQL-translatable; the last-name token match runs in memory
            // because Buyer is a single full-name string.
            // Repossessed accounts are closed deals — their balance stays positive (write-off
            // is a flag, not a zeroing) so !IsPaid alone would still offer them for payment.
            var candidates = _context.Accounts.AsNoTracking()
                .Include(a => a.Payments).Include(a => a.Sale).ThenInclude(s => s.Car)
                .Where(a => !a.IsPaid && !a.IsRepossessed && a.Sale.Car.VIN.EndsWith(last4)).ToList();

            Account account = candidates.FirstOrDefault(a =>
                a.Sale.Buyer.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name)
                || a.Sale.Buyer.ToLowerInvariant().EndsWith(name));

            if (account == null)
            {
                Found = false; // SAME message — no "not found" oracle.
                return Page();
            }

            Found = true;
            decimal payoff = account.Balance();
            AmountDue = PaymentMath.AmountDueNow(account.LateDue(), payoff);
            RemainingBalance = payoff;   // surfaced on the confirm card (the lookup already authenticated them)
            string buyer = account.Sale.Buyer?.Trim() ?? "";   // imported rows could be blank
            MaskedBuyer = buyer.Length > 0 ? $"{buyer[0]}. ••••" : "•••• ••••";
            CarLabel = account.Sale.Car.Name();
            VinMask = $"VIN •••• {account.Sale.Car.VIN[^4..]}";
            // Prefill the input with what we'd actually charge: this month's due, or a single
            // monthly payment for a paid-ahead account — NOT the full payoff.
            Amount = AmountDue > 0 ? AmountDue : Math.Min(account.MonthlyPayment, payoff);
            Token = _protector.Protect(account.Id.ToString(), TimeSpan.FromMinutes(15));
            return Page();
        }

        public async Task<IActionResult> OnPostPay()
        {
            if (!_checkout.IsConfigured)
            {
                Message = "Online payments are temporarily unavailable. Please call the dealership.";
                return Page();
            }

            string unprotected;
            try
            {
                unprotected = _protector.Unprotect(Token ?? "");
            }
            catch (CryptographicException)   // tampered or past the 15-minute expiry
            {
                Message = "Your session expired. Please look up your account again.";
                return Page();
            }
            // TryParse covers any non-int / overflow in the (already-authenticated) payload.
            if (!int.TryParse(unprotected, out int accountId))
            {
                Message = "Your session expired. Please look up your account again.";
                return Page();
            }

            Account account = _context.Accounts.AsNoTracking()
                .Include(a => a.Payments).Include(a => a.Sale).ThenInclude(s => s.Car)
                .FirstOrDefault(a => a.Id == accountId && !a.IsPaid && !a.IsRepossessed);
            if (account == null)
            {
                Message = "Account not available.";
                return Page();
            }

            // Derive the amount server-side; the client value is clamped, never trusted blindly.
            decimal payoff = account.Balance();
            decimal due = PaymentMath.AmountDueNow(account.LateDue(), payoff);
            // Paid-ahead fallback is one monthly payment, never the whole payoff.
            decimal defaultWhenNothingDue = Math.Min(account.MonthlyPayment, payoff);
            decimal amount = PaymentMath.ClampPayment(Amount, due, payoff, defaultWhenNothingDue);
            long cents = PaymentMath.ToCents(amount);
            if (cents < 50) // Stripe's minimum charge — possible when a residual payoff is under $0.50
            {
                Message = "Your remaining balance is too small to pay online. Please call us to settle it.";
                return Page();
            }

            // ForwardedHeaders gives the correct https scheme/host behind Railway's proxy.
            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            string url;
            try
            {
                url = await _checkout.CreateAchCheckoutUrlAsync(account.Id, cents, account.Sale.Buyers(), baseUrl);
            }
            catch (Stripe.StripeException ex) // API outage, or a residual payoff below Stripe's $0.50 minimum
            {
                _logger.LogError(ex, "Stripe Checkout creation failed for account {Acc} ({Cents} cents)", account.Id, cents);
                Message = "We couldn't start your payment just now. Please try again in a few minutes, or call us.";
                return Page();
            }
            _logger.LogInformation("Stripe Checkout created for account {Acc} ({Cents} cents)", account.Id, cents);
            return Redirect(url); // 302 to checkout.stripe.com (full page, not iframed)
        }
    }
}
