namespace LopezAutoSales.Server.Configuration
{
    // Bound from the "Stripe" config section. TEST keys come from user-secrets (dev)
    // or environment variables (prod): Stripe__SecretKey, Stripe__PublishableKey,
    // Stripe__WebhookSecret. appsettings.json ships empty placeholders. NEVER commit real keys.
    public class StripeOptions
    {
        public string SecretKey { get; set; }       // sk_test_…
        public string PublishableKey { get; set; }  // pk_test_…
        public string WebhookSecret { get; set; }   // whsec_…
    }
}
