using LopezAutoSales.Shared.Models;
using System;

namespace LopezAutoSales.Server.Models
{
    public class UserAccount
    {
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public int AccountId { get; set; }
        public Account Account { get; set; }

        public DateTime DateSet { get; set; }
    }
}
