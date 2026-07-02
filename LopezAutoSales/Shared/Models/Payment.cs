using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LopezAutoSales.Shared.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public Account Account { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;
        [Column(TypeName = "decimal(9,2)")]
        [Required]
        // Enforced server-side via [ApiController] model validation: a negative "payment"
        // would silently inflate the balance; zero would flip IsPaid on a zero balance.
        [Range(typeof(decimal), "0.01", "9999999.99", ErrorMessage = "Payment amount must be positive.")]
        public decimal Amount { get; set; }

        // Idempotency / audit key for online (Stripe) payments. Null for in-person
        // payments recorded via AccountController.AddPayment.
        public string StripePaymentIntentId { get; set; }

        [NotMapped]
        public string Reason { get; set; }
    }
}
