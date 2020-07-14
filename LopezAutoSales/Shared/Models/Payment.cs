using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace LopezAutoSales.Shared.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public string SaleId { get; set; }
        public Sale Sale { get; set; }

        public DateTime Date { get; set; }
        [Column(TypeName = "money")]
        public decimal Amount { get; set; }
    }
}
