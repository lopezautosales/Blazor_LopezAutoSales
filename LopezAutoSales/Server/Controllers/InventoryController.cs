using LopezAutoSales.Server.Data;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetInventory()
        {
            List<Car> cars = await _context.Cars.AsNoTracking().Where(x => x.IsListed).Include(x => x.Pictures).ToListAsync();
            if (!User.IsInRole("Admin"))
                cars.ForEach(x => x.BoughtPrice = null);
            return Ok(cars);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCar(int id)
        {
            Car car = await _context.Cars.AsNoTracking().Where(x => x.Id == id).Include(x => x.Pictures).FirstOrDefaultAsync();
            if (car == null)
                return BadRequest();
            if (!User.IsInRole("Admin"))
                car.BoughtPrice = null;
            return Ok(car);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddVehicle(Car car)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());

            car.IsListed = true;
            car.Date = DateTime.Now;
            _context.Cars.Add(car);
            _context.SaveChanges();
            return Ok(car.Id);
        }

        [HttpPut("edit/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditVehicle([FromRoute] int id, [FromBody] Car data)
        {
            Car car = await _context.Cars.FirstOrDefaultAsync(x => x.Id == id);
            if (car == null)
                return BadRequest();
            car.Update(data);
            car.IsSalvage = data.IsSalvage;
            car.JsonData = data.JsonData;
            car.ListPrice = data.ListPrice;
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost("sell")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SellVehicle(Sale sale)
        {
            if (!sale.HasLien)
                sale.Lienholder = null;
            if (!sale.HasTrade)
                sale.TradeIn = null;
            decimal due = sale.TotalDue();

            if (due == 0)
            {
                if (sale.HasLien)
                    ModelState.AddModelError(string.Empty, "Cannot have a lien on a paid vehicle.");
                sale.Account = null;
            }
            else if (due < 0)
                ModelState.AddModelError(string.Empty, "Total due cannot be less than 0.");
            else
                sale.Account.InitialDue = due;

            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
            Car car = await _context.Cars.Where(x => x.IsListed).Where(x => x.VIN == sale.Car.VIN).FirstOrDefaultAsync();
            if (car != null)
            {
                sale.Car.Id = car.Id;
                car.Update(sale.Car);
                car.IsListed = false;
                sale.Car = null;
            }
            if (sale.HasLien)
            {
                string normalized = sale.Lienholder.Name.ToUpper();
                Lienholder lienholder = _context.Lienholders.Find(normalized);
                if (lienholder != null)
                {
                    sale.LienholderNormalizedName = normalized;
                    lienholder.Update(sale.Lienholder);
                    sale.Lienholder = null;
                }
            }
            _context.Sales.Add(sale);
            _context.SaveChanges();
            return Ok(sale.Id);
        }
    }
}
