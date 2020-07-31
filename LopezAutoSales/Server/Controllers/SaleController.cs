using LopezAutoSales.Server.Data;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public SaleController(ApplicationDbContext context)
        {
            _context = context;
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

        [HttpPut("{id}")]
        public async Task<IActionResult> EditSale([FromRoute] int id, [FromBody] Sale data)
        {
            SetDue(data);
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
            //create new lienholder entry if different
            _context.Update(data);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> SellVehicle(Sale sale)
        {
            SetDue(sale);
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
            Car car = await _context.Cars.Where(x => x.IsListed).Where(x => x.VIN == sale.Car.VIN).FirstOrDefaultAsync();
            if (car != null)
            {
                sale.CarId = car.Id;
                car.Update(sale.Car);
                car.IsListed = false;
                sale.Car = null;
            }
            if (sale.HasLien)
            {
                string normalized = sale.Lienholder.Name.ToUpper();
                Lienholder lienholder = await _context.Lienholders.Where(x => x.NormalizedName == normalized).Include(x => x.Address).FirstOrDefaultAsync();
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

        private void SetDue(Sale sale)
        {
            decimal due = sale.TotalPayments();
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
        }
    }
}
