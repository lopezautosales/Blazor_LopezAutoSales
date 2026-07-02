using LopezAutoSales.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using System.Linq;
using System.Threading.Tasks;
using Account = LopezAutoSales.Shared.Models.Account;

namespace LopezAutoSales.Server.Pages
{
    // Landing page after Stripe Checkout — doubles as the customer's printable receipt
    // (the online equivalent of the printed payments screen handed out in-store).
    // Read-only: the webhook is the SOLE source of truth for recording a Payment.
    // Identity is masked exactly like /pay (first initial + VIN last-4); the unguessable
    // cs_… session id in the URL is what gates access to this page.
    // Rate-limited because OnGet makes an outbound Stripe API call per request.
    [EnableRateLimiting("pay-lookup")]
    public class SuccessModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IStripeCheckoutService _checkout;
        private readonly ILogger<SuccessModel> _logger;

        public SuccessModel(ApplicationDbContext context, IStripeCheckoutService checkout, ILogger<SuccessModel> logger)
        {
            _context = context;
            _checkout = checkout;
            _logger = logger;
        }

        public string Heading { get; set; } = "Thank you";
        public string Detail { get; set; } = "Your payment is being processed.";
        public decimal? AmountPaid { get; set; }   // proof of what was charged — the customer has nothing else on-screen
        public string Reference { get; set; }      // PaymentIntent id, quotable when calling the dealership
        public string MaskedBuyer { get; set; }
        public string CarLabel { get; set; }
        public string VinMask { get; set; }
        public decimal? Balance { get; set; }
        // True once OUR books contain this payment (card settles in seconds; ACH in days).
        // Until then the balance shown is pre-payment, and the receipt says so.
        public bool Settled { get; set; }

        public async Task<IActionResult> OnGet(string session_id)
        {
            // Cheap shape check before spending a Stripe API call on junk input.
            if (string.IsNullOrEmpty(session_id) || !session_id.StartsWith("cs_") || !_checkout.IsConfigured)
                return Page();

            try
            {
                Session session = await _checkout.GetSessionAsync(session_id);
                if (session.AmountTotal.HasValue)
                    AmountPaid = session.AmountTotal.Value / 100m;
                Reference = session.PaymentIntentId;
                switch (session.PaymentStatus)
                {
                    case "paid": // card clears immediately
                        Heading = "Payment received";
                        Detail = "Thank you — your payment was received.";
                        break;
                    default: // "unpaid" / "no_payment_required" / processing — typical for ACH
                        Heading = "Payment submitted";
                        Detail = "Bank transfers take a few business days to clear; we'll apply it once it settles.";
                        break;
                }

                // Receipt details from the account the session was created for (server-set
                // metadata — never client input).
                if (session.Metadata != null && session.Metadata.TryGetValue("accountId", out string accStr)
                    && int.TryParse(accStr, out int accountId))
                {
                    Account account = _context.Accounts.AsNoTracking()
                        .Include(a => a.Payments).Include(a => a.Sale).ThenInclude(s => s.Car)
                        .FirstOrDefault(a => a.Id == accountId);
                    if (account != null)
                    {
                        string buyer = account.Sale.Buyer?.Trim() ?? "";
                        MaskedBuyer = buyer.Length > 0 ? $"{buyer[0]}. ••••" : "•••• ••••";
                        CarLabel = account.Sale.Car.Name();
                        VinMask = $"VIN •••• {account.Sale.Car.VIN[^4..]}";
                        Balance = account.Balance();
                        Settled = Reference != null && account.Payments.Any(p => p.StripePaymentIntentId == Reference);
                    }
                }
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Could not retrieve Stripe session {Session}", session_id);
            }
            return Page();
        }
    }
}
