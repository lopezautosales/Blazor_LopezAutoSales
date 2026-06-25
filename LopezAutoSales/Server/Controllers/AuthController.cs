using LopezAutoSales.Server.Models;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet("me")]
        public async Task<ActionResult<CurrentUser>> Me()
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
                return Ok(new CurrentUser { IsAuthenticated = false });

            ApplicationUser user = await _userManager.GetUserAsync(User);
            return Ok(new CurrentUser
            {
                IsAuthenticated = true,
                Name = User.Identity.Name,
                Roles = (await _userManager.GetRolesAsync(user)).ToList()
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(
                request.Email, request.Password, request.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
                return Ok();
            if (result.IsLockedOut)
                return Unauthorized("Account locked. Try again later.");
            return Unauthorized("Invalid email or password.");
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok();
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            ApplicationUser user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            IdentityResult result = await _userManager.ChangePasswordAsync(
                user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                return BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));

            // Keep the current session valid after the credential change.
            await _signInManager.RefreshSignInAsync(user);
            return Ok();
        }
    }
}
