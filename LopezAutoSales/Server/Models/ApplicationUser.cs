using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace LopezAutoSales.Server.Models
{
    public class ApplicationUser : IdentityUser
    {
        public List<UserSale> Sales { get; set; } = new List<UserSale>();
    }
}
