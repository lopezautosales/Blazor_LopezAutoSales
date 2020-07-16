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
            List<Car> cars = await _context.Cars.AsNoTracking().Where(x => x.IsListed).Include(x => x.Images).ToListAsync();
            cars.ForEach(x => x.BoughtPrice = null);
            return Ok(cars);
        }

        [HttpGet]
        public async Task<IActionResult> GetWarranty(string id)
        {
            Car car = await _context.Cars.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            car.BoughtPrice = null;
            return Ok(car);
        }

        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminInventory()
        {
            List<Car> cars = await _context.Cars.AsNoTracking().Where(x => x.IsListed).Include(x => x.Images).ToListAsync();
            return Ok(cars);
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
    }
}
