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
using System.Text;

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
            // Payments must be included so the client can compute Balance()/LateDue()
            // per account without an N+1 round-trip (and without NRE on a null list).
            List<Account> accounts = _context.Accounts.AsNoTracking().Include(x => x.Payments).Include(x => x.Sale).ThenInclude(x => x.Car).OrderBy(x => x.Sale.Buyer).ToList();
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
                return NotFound(new string[] { "Could not find the account." });
            return Ok(account);
        }

        [HttpPost("payment")]
        [Authorize(Roles = "Admin")]
        public IActionResult AddPayment([FromBody] Payment data)
        {
            Account account = _context.Accounts.Include(x => x.Payments).Include(x => x.Sale).ThenInclude(x => x.Car).FirstOrDefault(x => x.Id == data.AccountId);
            if (account == null)
                return NotFound(new string[] { "Could not find the account." });
            if (data.Date.Date == DateTime.Today)
                data.Date = DateTime.Now;
            account.IsPaid = account.Balance() <= data.Amount;

            _logger.LogInformation($"{account.Sale.Buyers()} [{account.Sale.Car.Name()}]: PAYMENT {data.Date} {data.Amount}");
            Audit("PaymentAdded", $"{account.Sale.Buyers()} [{account.Sale.Car.Name()}] {data.Amount:C} dated {data.Date:d}");
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
                return NotFound(new string[] { "Could not find the account." });
            Payment payment = account.Payments.FirstOrDefault(x => x.Id == data.Id);
            if (payment == null)
                return NotFound(new string[] { "Payment was not found on this account." });

            if (data.Date.Date == DateTime.Today)
                data.Date = DateTime.Now;
            _logger.LogInformation($"{account.Sale.Buyers()} [{account.Sale.Car.Name()}]: ORIGINAL {payment.Date} {payment.Amount} EDIT {data.Date} {data.Amount} REASON {data.Reason}");
            Audit("PaymentEdited", $"{account.Sale.Buyers()} [{account.Sale.Car.Name()}] {payment.Amount:C}@{payment.Date:d} -> {data.Amount:C}@{data.Date:d}. Reason: {data.Reason}");
            payment.Amount = data.Amount;
            payment.Date = data.Date;
            account.IsPaid = account.Balance() <= 0;
            // account + payment are tracked; mutating them is enough — no Update() needed
            // (Update(account) would re-write the whole Sale/Car graph too).
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost("payment/delete")]
        [Authorize(Roles = "Admin")]
        public IActionResult RemovePayment([FromBody] Payment data)
        {
            Account account = _context.Accounts.Include(x => x.Payments).Include(x => x.Sale).ThenInclude(x => x.Car).FirstOrDefault(x => x.Id == data.AccountId);
            if (account == null)
                return NotFound(new string[] { "Could not find the account." });
            Payment payment = account.Payments.FirstOrDefault(x => x.Id == data.Id);
            if (payment == null)
                return NotFound(new string[] { "Payment was not found on this account." });
            account.Payments.Remove(payment);
            account.IsPaid = account.Balance() <= 0;
            _logger.LogInformation($"{account.Sale.Buyers()} [{account.Sale.Car.Name()}]: PAYMENT REMOVED {payment.Date} {payment.Amount} REASON {data.Reason}");
            Audit("PaymentRemoved", $"{account.Sale.Buyers()} [{account.Sale.Car.Name()}] {payment.Amount:C}@{payment.Date:d}. Reason: {data.Reason}");
            // account + payments are tracked; removing the payment is enough.
            _context.SaveChanges();
            return Ok();
        }

        // All payments collected in a year, as a downloadable CSV for the bookkeeper.
        [HttpGet("payments/{year}/csv")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetPaymentsCsv(int year)
        {
            DateTime start = new DateTime(year, 1, 1);
            DateTime end = start.AddYears(1);
            List<Payment> payments = _context.Payments.AsNoTracking()
                .Where(p => p.Date >= start && p.Date < end)
                .Include(p => p.Account).ThenInclude(a => a.Sale).ThenInclude(s => s.Car)
                .OrderBy(p => p.Date).ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Date,Buyers,Vehicle,Amount");
            foreach (Payment p in payments)
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(p.Date.ToShortDateString()), Csv(p.Account?.Sale?.Buyers()),
                    Csv(p.Account?.Sale?.Car?.Name()), p.Amount.ToString("0.00")
                }));
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"payments-{year}.csv");
        }

        // Minimal CSV field escaping (RFC 4180).
        private static string Csv(string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        // Append a durable audit record; committed in the same SaveChanges as the action.
        private void Audit(string action, string details)
            => _context.AuditLogs.Add(new AuditLog { Timestamp = DateTime.Now, User = User.Identity?.Name, Action = action, Details = details });
    }
}
