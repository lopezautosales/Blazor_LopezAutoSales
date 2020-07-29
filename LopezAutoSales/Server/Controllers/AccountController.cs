using LopezAutoSales.Server.Data;
using LopezAutoSales.Server.Models;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAccounts([FromQuery] bool showPaid = false)
        {
            List<Account> accounts = await _context.Accounts.AsNoTracking().Where(x => showPaid || x.IsPaid).Include(x => x.Sale).ToListAsync();
            return Ok(accounts);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetAccount(int id)
        {
            if (!User.IsInRole("Admin"))
            {
                string userId = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                    return BadRequest(new string[] { "Could not find user id." });
                UserAccount userAccount = await _context.UserAccounts.AsNoTracking().Where(x => x.UserId == userId).Where(x => x.AccountId == id).FirstOrDefaultAsync();
                if (userAccount == null)
                    return BadRequest(new string[] { "User is not authorized for viewing this account." });
            }
            Account account = await _context.Accounts.AsNoTracking().Where(x => x.Id == id).Include(x => x.Payments).Include(x => x.Sale).FirstOrDefaultAsync();
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            return Ok(account);
        }

        [HttpPost("payment/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddPayment([FromRoute] int id, [FromBody] Payment payment)
        {
            Account account = await _context.Accounts.FindAsync(id);
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            payment.AccountId = account.Id;
            if (payment.Date.Date == DateTime.Today)
                payment.Date = DateTime.Now;
            _context.Payments.Add(payment);
            _context.SaveChanges();
            return Ok(payment.Id);
        }

        [HttpDelete("payment/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemovePayment([FromRoute] int id)
        {
            Payment payment = await _context.Payments.FindAsync(id);
            if (payment == null)
                return BadRequest(new string[] { "Payment was not found." });
            _context.Payments.Remove(payment);
            _context.SaveChanges();
            return Ok();
        }
    }
}
