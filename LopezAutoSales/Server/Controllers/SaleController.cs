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
            List<Sale> sales = _context.Sales.AsNoTracking().Where(x => x.Date >= start && x.Date < end).OrderByDescending(x => x.Date).Include(x => x.Car).ToList();
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
            _logger.LogInformation($"{User.Identity?.Name} EDITED SALE {data.Id} {data.Buyers()} {data.Car?.Name()}");
            SetLien(data);
            _context.Update(data);
            // The sold car's cost basis is managed via inventory, never a sale edit:
            // Update(graph) would otherwise overwrite it from the posted values.
            if (data.Car != null)
                _context.Entry(data.Car).Property(x => x.BoughtPrice).IsModified = false;
            _context.SaveChanges();
            return Ok();
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
            sale.Car.BoughtPrice = null;
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public IActionResult SellVehicle(Sale sale)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
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
