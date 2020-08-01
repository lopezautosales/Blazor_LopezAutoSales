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
            List<Account> accounts = await _context.Accounts.AsNoTracking().Where(x => showPaid || !x.IsPaid).Include(x => x.Sale).ThenInclude(x => x.Car).ToListAsync();
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
            Account account = await _context.Accounts.AsNoTracking().Where(x => x.Id == id).Include(x => x.Payments).Include(x => x.Sale).ThenInclude(x => x.Car).FirstOrDefaultAsync();
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            return Ok(account);
        }

        [HttpPost("payment")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddPayment([FromBody] Payment data)
        {
            Account account = await _context.Accounts.Where(x => x.Id == data.AccountId).Include(x => x.Payments).FirstOrDefaultAsync();
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            if (data.Date.Date == DateTime.Today)
                data.Date = DateTime.Now;

            account.IsPaid = account.Balance() <= 0;
            _context.Payments.Add(data);
            _context.SaveChanges();
            return Ok(data.Id);
        }

        [HttpPut("payment")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditPayment(Payment data)
        {
            Account account = await _context.Accounts.Where(x => x.Id == data.AccountId).Include(x => x.Payments).FirstOrDefaultAsync();
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            Payment payment = account.Payments.FirstOrDefault(x => x.Id == data.Id);

            if (data.Date.Date == DateTime.Today)
                data.Date = DateTime.Now;

            account.IsPaid = account.Balance() <= 0;
            payment.Amount = data.Amount;
            payment.Date = data.Date;
            //log reason
            _context.Accounts.Update(account);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost("payment/delete")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemovePayment([FromBody] Payment data)
        {
            Account account = await _context.Accounts.Where(x => x.Id == data.AccountId).Include(x => x.Payments).FirstOrDefaultAsync();
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            Payment payment = account.Payments.FirstOrDefault(x => x.Id == data.Id);

            account.IsPaid = account.Balance() <= payment.Amount;
            //log reason
            _context.Payments.Remove(payment);
            _context.SaveChanges();
            return Ok();
        }
    }
}
