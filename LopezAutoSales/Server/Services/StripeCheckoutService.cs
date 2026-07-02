using LopezAutoSales.Server.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Services
{
    public interface IStripeCheckoutService
    {
        // False when Stripe keys are unset (payments are optional — the app still runs).
        bool IsConfigured { get; }
        Task<string> CreateAchCheckoutUrlAsync(int accountId, long amountCents, string buyerLabel, string baseUrl);
        Task<Session> GetSessionAsync(string sessionId);
    }

    // Thin wrapper over the Stripe SDK so PageModels/tests don't bind to static config.
    // Builds a StripeClient from options (no global mutable StripeConfiguration.ApiKey).
    public class StripeCheckoutService : IStripeCheckoutService
    {
        private readonly SessionService _sessions;

        public StripeCheckoutService(IOptions<StripeOptions> opts)
        {
            string key = opts.Value?.SecretKey;
            IsConfigured = !string.IsNullOrWhiteSpace(key);
            // Only build a client when keys exist; otherwise the service stays inert and
            // callers gate on IsConfigured. A bogus client would throw on first use anyway.
            if (IsConfigured)
                _sessions = new SessionService(new StripeClient(key));
        }

        public bool IsConfigured { get; }

        public async Task<string> CreateAchCheckoutUrlAsync(int accountId, long amountCents, string buyerLabel, string baseUrl)
        {
            SessionCreateOptions options = new SessionCreateOptions
            {
                Mode = "payment",
                // payment_method_types is deliberately OMITTED — that enables Stripe's
                // dynamic payment methods: which methods appear (ACH, card, Cash App, …)
                // and their order is managed in the Dashboard, no deploy needed. ACH
                // Direct Debit must be enabled there or bank debit won't be offered.
                // See docs/stripe-go-live.md.
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            UnitAmount = amountCents,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Auto note payment — {buyerLabel}"
                            }
                        }
                    }
                },
                PaymentMethodOptions = new SessionPaymentMethodOptionsOptions
                {
                    UsBankAccount = new SessionPaymentMethodOptionsUsBankAccountOptions
                    {
                        // Instant Financial Connections, falls back to microdeposits.
                        VerificationMethod = "automatic",
                        FinancialConnections = new SessionPaymentMethodOptionsUsBankAccountFinancialConnectionsOptions
                        {
                            Permissions = new List<string> { "payment_method" }
                        }
                    }
                },
                // accountId is the ONLY trusted link payment->account; set server-side,
                // read back in the webhook. Never trusted from the client.
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Metadata = new Dictionary<string, string> { ["accountId"] = accountId.ToString() }
                },
                Metadata = new Dictionary<string, string> { ["accountId"] = accountId.ToString() },
                ClientReferenceId = accountId.ToString(),
                SuccessUrl = $"{baseUrl}/pay/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{baseUrl}/pay/cancelled",
                // The amount is frozen at session creation; Stripe's default expiry is 24h,
                // during which an in-person payment could make the frozen amount an overcharge.
                // Stripe's minimum is 30 minutes — 35 leaves headroom for clock skew.
                ExpiresAt = DateTime.UtcNow.AddMinutes(35),
            };
            Session session = await _sessions.CreateAsync(options);
            return session.Url;
        }

        public Task<Session> GetSessionAsync(string sessionId) => _sessions.GetAsync(sessionId);
    }
}
