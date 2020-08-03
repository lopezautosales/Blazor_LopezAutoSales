using IdentityServer4.Extensions;
using LopezAutoSales.Server.Data;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        [HttpGet]
        public async Task<IActionResult> GetSales()
        {
            List<Sale> sales = await _context.Sales.Include(x => x.Car).ToListAsync();
            return Ok(sales);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSale(int id)
        {
            Sale sale = await _context.Sales.Where(x => x.Id == id).Include(x => x.Account).Include(x => x.Car).Include(x => x.TradeIn).Include(x => x.Address).Include(x => x.Lienholder).ThenInclude(x => x.Address).FirstOrDefaultAsync();
            if (sale == null)
                return BadRequest(new string[] { "Account was not found." });
            return Ok(sale);
        }

        [HttpPut]
        public IActionResult EditSale([FromBody] Sale data)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
            _logger.LogInformation($"{User.GetDisplayName()} EDITED SALE {data.Id} {data.Buyers()} {data.Car.Name}");
            SetLien(data);
            _context.Update(data);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> SellVehicle(Sale sale)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
            Car car = await _context.Cars.Where(x => x.IsListed).Where(x => x.VIN == sale.Car.VIN).FirstOrDefaultAsync();
            if (car != null)
            {
                sale.CarId = car.Id;
                car.IsListed = false;
            }
            SetLien(sale);
            _context.Sales.Add(sale);
            _context.SaveChanges();
            _logger.LogInformation($"{User.GetDisplayName()} SALE {sale.Id} {sale.Buyers()} {sale.Car.Name}");
            return Ok(sale.Id);
        }

        private void SetLien(Sale sale)
        {
            if (sale.HasLien)
                sale.LienholderNormalizedName = sale.Lienholder.Name.ToUpper();
        }
    }
}
