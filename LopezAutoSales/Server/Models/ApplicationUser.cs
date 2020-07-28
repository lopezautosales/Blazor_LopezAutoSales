using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace LopezAutoSales.Server.Models
{
    public class ApplicationUser : IdentityUser
    {
        public List<UserAccount> Sales { get; set; } = new List<UserAccount>();
    }
}
