using System;

namespace LopezAutoSales.Server.Services
{
    // Pure money logic for the public /pay flow, factored out so it can be unit-tested
    // without a DbContext or HTTP context. All amounts are dollars (decimal); cents are
    // computed once at the Stripe boundary.
    public static class PaymentMath
    {
        // The amount we show/charge as "due now": this month's LateDue, never below zero
        // and never more than the remaining Balance (payoff). Never exposes the payoff itself.
        public static decimal AmountDueNow(decimal lateDue, decimal balance)
            => Math.Max(0m, Math.Min(lateDue, balance));

        // Clamp a customer-entered amount to the range [$1, payoff]. A missing/non-positive
        // request falls back to the amount due, or the payoff if nothing is currently due.
        // The client value is never trusted blindly — it is always clamped server-side.
        public static decimal ClampPayment(decimal? requested, decimal due, decimal payoff)
        {
            decimal r = (requested.HasValue && requested.Value > 0)
                ? requested.Value
                : (due > 0 ? due : payoff);
            return Math.Min(Math.Max(r, 1m), payoff);
        }

        // Stripe wants the smallest currency unit (cents), rounded away from zero.
        public static long ToCents(decimal amount)
            => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
    }
}
