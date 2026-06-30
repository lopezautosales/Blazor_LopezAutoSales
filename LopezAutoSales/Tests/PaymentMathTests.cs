using LopezAutoSales.Server.Services;
using Xunit;

namespace LopezAutoSales.Tests
{
    // Pure money logic behind the public /pay flow (amount clamping the customer can't bypass).
    public class PaymentMathTests
    {
        [Theory]
        [InlineData(300, 1200, 300)]   // normal: this month's due, well under payoff
        [InlineData(900, 500, 500)]    // due exceeds remaining balance -> capped at payoff
        [InlineData(-50, 1200, 0)]     // overpaid this cycle (negative LateDue) -> never negative
        public void AmountDueNow_clamps_between_zero_and_payoff(decimal lateDue, decimal balance, decimal expected)
        {
            Assert.Equal(expected, PaymentMath.AmountDueNow(lateDue, balance));
        }

        [Fact]
        public void ClampPayment_uses_due_when_no_amount_entered()
        {
            Assert.Equal(300m, PaymentMath.ClampPayment(null, due: 300m, payoff: 1200m, defaultWhenNothingDue: 250m));
        }

        [Fact]
        public void ClampPayment_falls_back_to_monthly_default_when_nothing_currently_due()
        {
            // Paid-ahead account: a blank/zero submit must NOT charge the full payoff —
            // it falls back to the supplied monthly default (one payment).
            Assert.Equal(250m, PaymentMath.ClampPayment(null, due: 0m, payoff: 1200m, defaultWhenNothingDue: 250m));
        }

        [Fact]
        public void ClampPayment_never_exceeds_payoff_even_if_customer_overstates()
        {
            // A customer typing 9999 can never charge more than what they owe.
            Assert.Equal(1200m, PaymentMath.ClampPayment(9999m, due: 300m, payoff: 1200m, defaultWhenNothingDue: 250m));
        }

        [Fact]
        public void ClampPayment_allows_explicit_early_payoff()
        {
            // Typing the payoff amount is honored exactly (early payoff stays possible).
            Assert.Equal(1200m, PaymentMath.ClampPayment(1200m, due: 0m, payoff: 1200m, defaultWhenNothingDue: 250m));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public void ClampPayment_floors_non_positive_requests_to_a_dollar(decimal requested)
        {
            // 0/negative requested falls back to due; with due=0 it uses the monthly default,
            // so test the floor with a tiny default and payoff.
            Assert.Equal(1m, PaymentMath.ClampPayment(requested, due: 0m, payoff: 1m, defaultWhenNothingDue: 0m));
        }

        [Theory]
        [InlineData(300.00, 30000)]
        [InlineData(123.45, 12345)]
        [InlineData(0.005, 1)]   // rounds away from zero
        public void ToCents_rounds_to_smallest_unit(decimal dollars, long expectedCents)
        {
            Assert.Equal(expectedCents, PaymentMath.ToCents(dollars));
        }
    }
}
