using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LopezAutoSales.Shared.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public int? LienholderId { get; set; }
        public Lienholder Lienholder { get; set; }
        public int AddressId { get; set; }
        public Address Address { get; set; }
        public int CarId { get; set; }
        public Car Car { get; set; }
        public int? TradeInId { get; set; }
        public Car TradeIn { get; set; }
        public int? AccountId { get; set; }
        public Account Account { get; set; }

        public DateTime Date { get; set; }
        [Required]
        public string Phone { get; set; }
        [Required]
        public string Buyer { get; set; }
        public string CoBuyer { get; set; }
        [Column(TypeName = "decimal(9,2)")]
        public decimal SellingPrice { get; set; }
        [Column(TypeName = "decimal(9,2)")]
        public decimal DownPayment { get; set; }
        [Column(TypeName = "decimal(5,3)")]
        public decimal TaxRate { get; set; } = Dealership.TaxRate;
        public int? Warranty { get; set; } = Dealership.Warranty;
        [Column(TypeName = "decimal(9,2)")]
        public decimal FinanceCharge { get; set; }
        public bool HasTag { get; set; }
        [Required]
        [Column(TypeName = "decimal(9,2)")]
        public decimal TagAmount
        {
            get
            {
                return HasTag ? tagAmount : 0;
            }
            set
            {
                tagAmount = value;
            }
        }
        private decimal tagAmount = Dealership.TagAmount;
        public bool HasLien { get; set; }
        [Required]
        [Column(TypeName = "decimal(9,2)")]
        public decimal LienAmount
        {
            get
            {
                return HasLien ? lienAmount : 0;
            }
            set
            {
                lienAmount = value;
            }
        }
        private decimal lienAmount = Dealership.LienAmount;
        public bool IsOutOfState { get; set; }

        public decimal TradeDifference()
        {
            return SellingPrice - (TradeIn?.BoughtPrice ?? 0);
        }

        public decimal TaxAmount()
        {
            if (IsOutOfState)
                return 0;
            return Math.Round(TradeDifference() * TaxRate / 100, 2);
        }

        public decimal Subtotal()
        {
            decimal subtotal = TradeDifference() + TaxAmount();
            if (HasTag)
                subtotal += TagAmount;
            if (HasLien)
                subtotal += LienAmount;
            return subtotal;
        }

        public decimal TotalDue()
        {
            return Subtotal() - DownPayment;
        }

        public int MonthsToPay()
        {
            decimal total = TotalDue();
            if (total <= 0)
                return 0;
            if (Account == null || Account.MonthlyPayment == 0)
                return 0;
            return (int)Math.Ceiling(total / Account.MonthlyPayment);
        }

        public decimal TotalPayments()
        {
            return TotalDue() + FinanceCharge;
        }

        [NotMapped]
        public bool HasTrade { get; set; }
    }
}
