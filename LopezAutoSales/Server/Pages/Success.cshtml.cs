using LopezAutoSales.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Pages
{
    // Landing page after Stripe Checkout. Shows status only — the webhook is the SOLE
    // source of truth for recording a Payment. Nothing is written here.
    // Rate-limited because OnGet makes an outbound Stripe API call per request.
    [EnableRateLimiting("pay-lookup")]
    public class SuccessModel : PageModel
    {
        private readonly IStripeCheckoutService _checkout;
        private readonly ILogger<SuccessModel> _logger;

        public SuccessModel(IStripeCheckoutService checkout, ILogger<SuccessModel> logger)
        {
            _checkout = checkout;
            _logger = logger;
        }

        public string Heading { get; set; } = "Thank you";
        public string Detail { get; set; } = "Your payment is being processed.";
        public decimal? AmountPaid { get; set; }   // proof of what was charged — the customer has nothing else on-screen
        public string Reference { get; set; }      // PaymentIntent id, quotable when calling the dealership

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
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Could not retrieve Stripe session {Session}", session_id);
            }
            return Page();
        }
    }
}
