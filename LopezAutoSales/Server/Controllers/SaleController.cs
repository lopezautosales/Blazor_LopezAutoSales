using LopezAutoSales.Server.Models;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LopezAutoSales.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class SaleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SaleController> _logger;

        public SaleController(ApplicationDbContext context, ILogger<SaleController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("{year}")]
        public IActionResult GetSales(int year)
        {
            DateTime start = new DateTime(year, 1, 1);
            DateTime end = start.AddYears(1);
            List<Sale> sales = _context.Sales.AsNoTracking().Where(x => x.Date >= start && x.Date < end).OrderByDescending(x => x.Date).Include(x => x.Car).Include(x => x.Account).ToList();
            return Ok(sales);
        }

        [HttpGet("view/{id}")]
        public IActionResult GetSale(int id)
        {
            Sale sale = _context.Sales.AsNoTracking().Include(x => x.Account).Include(x => x.Car).Include(x => x.TradeIn).Include(x => x.Address).Include(x => x.Lienholder).ThenInclude(x => x.Address).FirstOrDefault(x => x.Id == id);
            if (sale == null)
                return NotFound(new string[] { "Sale was not found." });
            return Ok(sale);
        }

        [HttpPut]
        public IActionResult EditSale([FromBody] Sale data)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
            if (data.TotalDue() < 0)
                return BadRequest(new string[] { "Total due cannot be negative." });
            _logger.LogInformation($"{User.Identity?.Name} EDITED SALE {data.Id} {data.Buyers()} {data.Car?.Name()}");
            Audit("SaleEdited", $"Sale {data.Id} {data.Buyers()} {data.Car?.Name()}");
            SetLien(data);
            _context.Update(data);
            // The sold car's cost basis is managed via inventory, never a sale edit:
            // Update(graph) would otherwise overwrite it from the posted values.
            if (data.Car != null)
                _context.Entry(data.Car).Property(x => x.BoughtPrice).IsModified = false;
            // Repossession state is managed via /api/account/repossess, never a sale edit —
            // the posted account carries whatever was loaded when the form opened.
            if (data.Account != null)
            {
                _context.Entry(data.Account).Property(x => x.IsRepossessed).IsModified = false;
                _context.Entry(data.Account).Property(x => x.RepossessedDate).IsModified = false;
            }
            using var transaction = _context.Database.BeginTransaction();
            _context.SaveChanges();
            ReconcileAccount(data.Id);
            transaction.Commit();
            return Ok();
        }

        // An edited sale changes what's owed; recompute the account from the saved sale so
        // the client can't strand it (a sale edited down to cash would leave an active,
        // /pay-able account) or desync it (a raised price would leave IsPaid=true hiding
        // the new balance). Client-posted account money is never trusted.
        private void ReconcileAccount(int saleId)
        {
            Sale sale = _context.Sales.Include(x => x.Account).ThenInclude(a => a.Payments)
                .Include(x => x.TradeIn).First(x => x.Id == saleId);
            Account account = sale.Account;
            if (sale.TotalDue() <= 0)
            {
                if (account == null)
                    return;
                if (account.Payments.Count == 0)
                {
                    _context.Accounts.Remove(account); // nothing owed, nothing paid — no account
                }
                else
                {
                    // Payments exist; keep the history but close the account.
                    account.InitialDue = sale.TotalPayments();
                    account.IsPaid = true;
                }
            }
            else
            {
                if (account == null)
                {
                    account = new Account { SaleId = sale.Id, Payments = new List<Payment>() };
                    _context.Accounts.Add(account);
                }
                // TotalPayments = TotalDue + FinanceCharge — matches the contract's
                // "Total of Payments" disclosure and SaleForm's client-side value.
                account.InitialDue = sale.TotalPayments();
                account.IsPaid = account.Balance() <= 0;
            }
            if (_context.ChangeTracker.HasChanges())
                _context.SaveChanges();
        }

        [HttpPut("boughtPrice/{id}")]
        public IActionResult SetBoughtPrice(int id, [FromBody] decimal amount)
        {
            if (amount < 0)
                return BadRequest("Bought price cannot be negative.");

            Sale sale = _context.Sales.Include(x => x.Car).FirstOrDefault(x => x.Id == id);

            if (sale is null || sale.Car is null)
                return BadRequest("Could not load the given sale/car.");

            _logger.LogInformation($"{User.Identity?.Name} SET BOUGHT PRICE {id} - {amount}");
            Audit("BoughtPriceSet", $"Sale {id} [{sale.Car.Name()}] -> {amount:C}");
            sale.Car.BoughtPrice = amount;
            _context.SaveChanges();
            return Ok();
        }

        [HttpDelete("boughtPrice/{id}")]
        public IActionResult RemoveBoughtPrice(int id)
        {
            Sale sale = _context.Sales.Include(x => x.Car).FirstOrDefault(x => x.Id == id);

            if (sale is null || sale.Car is null)
                return BadRequest("Could not load the given sale/car.");

            _logger.LogInformation($"{User.Identity?.Name} REMOVED BOUGHT PRICE {id}");
            Audit("BoughtPriceRemoved", $"Sale {id} [{sale.Car.Name()}]");
            sale.Car.BoughtPrice = null;
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public IActionResult SellVehicle(Sale sale)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
            if (sale.TotalDue() < 0)
                return BadRequest(new string[] { "Total due cannot be negative." });
            // Recompute account money server-side; the client's values are never trusted.
            if (sale.TotalDue() == 0)
                sale.Account = null;
            else if (sale.Account != null)
            {
                sale.Account.InitialDue = sale.TotalPayments();
                sale.Account.IsPaid = false;
            }
            if (sale.Date.Date == DateTime.Today)
                sale.Date = DateTime.Now;

            // Capture before the matched-car branch nulls sale.Car below.
            string carName = sale.Car.Name();
            Car car = _context.Cars.Where(x => x.IsListed).FirstOrDefault(x => x.VIN == sale.Car.VIN);
            if (car != null)
            {
                sale.CarId = car.Id;
                car.Update(sale.Car);
                car.IsListed = false;
                sale.Car = null;
            }
            SetLien(sale);
            _context.Sales.Add(sale);
            Audit("SaleCreated", $"{sale.Buyers()} {carName}");
            _context.SaveChanges();
            _logger.LogInformation($"{User.Identity?.Name} SALE {sale.Id} {sale.Buyers()} {carName}");
            return Ok(sale.Id);
        }

        [HttpGet("report/{year}")]
        public IActionResult GetYearlyReport(int year)
        {
            DateTime start = new DateTime(year, 1, 1);
            DateTime end = start.AddYears(1);
            return Ok(_context.Sales.Where(x => x.Date >= start && x.Date < end).Include(x => x.Account).Include(x => x.Car).Include(x => x.TradeIn).OrderBy(x => x.Date).AsNoTracking().ToList());
        }

        // Append a durable audit record; committed in the same SaveChanges as the action.
        private void Audit(string action, string details)
            => _context.AuditLogs.Add(new AuditLog { Timestamp = DateTime.Now, User = User.Identity?.Name, Action = action, Details = details });

        private void SetLien(Sale sale)
        {
            if (sale.HasLien && sale.Lienholder != null)
            {
                sale.LienholderNormalizedName = sale.Lienholder.Name.ToUpper();
                Lienholder lienholder = _context.Lienholders.Include(x => x.Address).FirstOrDefault(x => x.NormalizedName == sale.LienholderNormalizedName);
                if (lienholder != null)
                {
                    lienholder.Update(sale.Lienholder);
                    sale.Lienholder = null;
                }
            }
        }
    }
}
