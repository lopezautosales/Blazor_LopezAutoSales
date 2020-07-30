using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace LopezAutoSales.Shared.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public Account Account { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
        [Column(TypeName = "decimal(9,2)")]
        public decimal Amount { get; set; }
    }
}
