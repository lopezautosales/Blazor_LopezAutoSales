using LopezAutoSales.Server.Models;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace LopezAutoSales.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult GetAccounts()
        {
            List<Account> accounts = _context.Accounts.AsNoTracking().Include(x => x.Sale).ThenInclude(x => x.Car).OrderBy(x => x.Sale.Buyer).ToList();
            return Ok(accounts);
        }

        [HttpGet("{id}")]
        [Authorize]
        public IActionResult GetAccount(int id)
        {
            if (!User.IsInRole("Admin"))
            {
                string userId = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                    return BadRequest(new string[] { "Could not find user id." });
                UserAccount userAccount = _context.UserAccounts.AsNoTracking().Where(x => x.AccountId == id).FirstOrDefault(x => x.UserId == userId);
                if (userAccount == null)
                    return BadRequest(new string[] { "User is not authorized for viewing this account." });
            }
            Account account = _context.Accounts.AsNoTracking().Include(x => x.Payments).Include(x => x.Sale).ThenInclude(x => x.Car).FirstOrDefault(x => x.Id == id);
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            return Ok(account);
        }

        [HttpPost("payment")]
        [Authorize(Roles = "Admin")]
        public IActionResult AddPayment([FromBody] Payment data)
        {
            Account account = _context.Accounts.Include(x => x.Payments).Include(x => x.Sale).ThenInclude(x => x.Car).FirstOrDefault(x => x.Id == data.AccountId);
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            if (data.Date.Date == DateTime.Today)
                data.Date = DateTime.Now;
            account.IsPaid = account.Balance() <= data.Amount;

            _logger.LogInformation($"{account.Sale.Buyers()} [{account.Sale.Car.Name()}]: PAYMENT {data.Date} {data.Amount}");
            _context.Payments.Add(data);
            _context.SaveChanges();
            return Ok(data.Id);
        }

        [HttpPut("payment")]
        [Authorize(Roles = "Admin")]
        public IActionResult EditPayment(Payment data)
        {
            Account account = _context.Accounts.Include(x => x.Payments).Include(x => x.Sale).ThenInclude(x => x.Car).FirstOrDefault(x => x.Id == data.AccountId);
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            Payment payment = account.Payments.First(x => x.Id == data.Id);

            if (data.Date.Date == DateTime.Today)
                data.Date = DateTime.Now;
            _logger.LogInformation($"{account.Sale.Buyers()} [{account.Sale.Car.Name()}]: ORIGINAL {payment.Date} {payment.Amount} EDIT {data.Date} {data.Amount} REASON {data.Reason}");
            payment.Amount = data.Amount;
            payment.Date = data.Date;
            account.IsPaid = account.Balance() <= 0;
            _context.Accounts.Update(account);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost("payment/delete")]
        [Authorize(Roles = "Admin")]
        public IActionResult RemovePayment([FromBody] Payment data)
        {
            Account account = _context.Accounts.Include(x => x.Payments).Include(x => x.Sale).ThenInclude(x => x.Car).FirstOrDefault(x => x.Id == data.AccountId);
            if (account == null)
                return BadRequest(new string[] { "Could not find the account." });
            Payment payment = account.Payments.First(x => x.Id == data.Id);
            account.Payments.Remove(payment);
            account.IsPaid = account.Balance() <= 0;
            _logger.LogInformation($"{account.Sale.Buyers()} [{account.Sale.Car.Name()}]: PAYMENT REMOVED {payment.Date} {payment.Amount} REASON {data.Reason}");
            _context.Accounts.Update(account);
            _context.SaveChanges();
            return Ok();
        }
    }
}
