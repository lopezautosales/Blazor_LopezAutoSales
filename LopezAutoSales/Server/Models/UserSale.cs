using LopezAutoSales.Shared.Models;
using System;

namespace LopezAutoSales.Server.Models
{
    public class UserSale
    {
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string SaleId { get; set; }
        public Sale Sale { get; set; }

        public DateTime DateSet { get; set; }
    }
}
