using LopezAutoSales.Shared.Models;
using Xunit;

namespace LopezAutoSales.Tests
{
    public class SaleMathTests
    {
        // Explicit values so the tests don't depend on Dealership defaults.
        private static Sale NewSale() => new Sale
        {
            SellingPrice = 10000m,
            PaperworkFee = 125m,
            TaxRate = 8.5m,
            DownPayment = 0m,
            IsOutOfState = false,
            HasTag = false,
            HasLien = false,
            Buyer = "John Buyer"
        };

        [Fact]
        public void TradeDifference_is_selling_price_when_no_trade_in()
        {
            Assert.Equal(10000m, NewSale().TradeDifference());
        }

        [Fact]
        public void TradeDifference_subtracts_trade_in_value()
        {
            Sale s = NewSale();
            s.TradeIn = new Car { BoughtPrice = 3000m };
            Assert.Equal(7000m, s.TradeDifference());
        }

        [Fact]
        public void TaxAmount_taxes_the_trade_difference_not_the_full_price()
        {
            Sale s = NewSale();
            s.TradeIn = new Car { BoughtPrice = 3000m }; // taxable base 7000
            Assert.Equal(595.00m, s.TaxAmount());        // 7000 * 8.5%
        }

        [Fact]
        public void TaxAmount_is_zero_out_of_state()
        {
            Sale s = NewSale();
            s.IsOutOfState = true;
            Assert.Equal(0m, s.TaxAmount());
        }

        [Fact]
        public void TaxAmount_rounds_to_cents()
        {
            Sale s = NewSale();
            s.SellingPrice = 100.10m;            // 100.10 * 8.5% = 8.5085
            Assert.Equal(8.51m, s.TaxAmount());
        }

        [Fact]
        public void Subtotal_sums_difference_tax_and_paperwork()
        {
            // 10000 + (10000*8.5%=850) + 125 = 10975
            Assert.Equal(10975m, NewSale().Subtotal());
        }

        [Fact]
        public void Subtotal_includes_tag_and_lien_only_when_enabled()
        {
            Sale s = NewSale();
            s.HasTag = true; s.TagAmount = 20m;
            s.HasLien = true; s.LienAmount = 20m;
            Assert.Equal(11015m, s.Subtotal()); // 10975 + 20 + 20
        }

        [Fact]
        public void Disabled_tag_and_lien_amounts_read_as_zero()
        {
            Sale s = NewSale();
            s.TagAmount = 20m;   // HasTag false
            s.LienAmount = 20m;  // HasLien false
            Assert.Equal(0m, s.TagAmount);
            Assert.Equal(0m, s.LienAmount);
        }

        [Fact]
        public void TotalDue_subtracts_the_down_payment()
        {
            Sale s = NewSale();
            s.DownPayment = 5000m;
            Assert.Equal(5975m, s.TotalDue()); // 10975 - 5000
        }

        [Fact]
        public void MonthsToPay_rounds_up_against_the_monthly_payment()
        {
            Sale s = NewSale();
            s.Account = new Account { MonthlyPayment = 300m }; // 10975 / 300 = 36.58 -> 37
            Assert.Equal(37, s.MonthsToPay());
        }

        [Fact]
        public void MonthsToPay_is_zero_when_paid_in_full_or_no_account()
        {
            Sale paid = NewSale();
            paid.DownPayment = 20000m; // total due negative
            paid.Account = new Account { MonthlyPayment = 300m };
            Assert.Equal(0, paid.MonthsToPay());

            Sale noAccount = NewSale();
            Assert.Equal(0, noAccount.MonthsToPay());
        }

        [Fact]
        public void TotalPayments_adds_the_finance_charge()
        {
            Sale s = NewSale();
            s.FinanceCharge = 1500m;
            Assert.Equal(12475m, s.TotalPayments()); // 10975 + 1500
        }

        [Theory]
        [InlineData("John", null, "John")]
        [InlineData("John", "", "John")]
        [InlineData("John", "Jane", "John & Jane")]
        public void Buyers_joins_co_buyer_when_present(string buyer, string coBuyer, string expected)
        {
            Sale s = new Sale { Buyer = buyer, CoBuyer = coBuyer };
            Assert.Equal(expected, s.Buyers());
        }
    }
}
