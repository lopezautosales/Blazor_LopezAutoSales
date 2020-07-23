using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LopezAutoSales.Shared.Models
{
    public class Account
    {
        public int Id { get; set; }

        public bool IsPaid { get; set; }
        [Required]
        [Column(TypeName = "decimal(9,2)")]
        public decimal InitialDue { get; set; }
        public IEnumerable<Payment> Payments { get; set; }
        [Required]
        [Column(TypeName = "decimal(9,2)")]
        public decimal MonthlyPayment { get; set; }
    }
}
