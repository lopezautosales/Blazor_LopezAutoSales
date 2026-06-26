using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LopezAutoSales.Shared.Models
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }

    public class CurrentUser
    {
        public bool IsAuthenticated { get; set; }
        public string Name { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; }

        [Required]
        [MinLength(8, ErrorMessage = "The new password must be at least 8 characters.")]
        public string NewPassword { get; set; }

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
