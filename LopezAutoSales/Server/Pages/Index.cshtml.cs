using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace LopezAutoSales.Server.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public List<Car> Cars { get; set; }
        public string Sort { get; set; }

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet(string sort)
        {
            Sort = sort;
            // Pictures keep their raw keys; the grid builds Cloudflare resize URLs from them.
            IQueryable<Car> query = _context.Cars.AsNoTracking().Where(x => x.IsListed).Include(x => x.Pictures);
            // Default to cheapest-first — the audience shops on monthly payment, so leading
            // with the priciest cars was backwards.
            query = sort switch
            {
                "price-high" => query.OrderByDescending(x => x.ListPrice),
                "newest" => query.OrderByDescending(x => x.Year),
                _ => query.OrderBy(x => x.ListPrice),
            };
            Cars = query.ToList();
            return Page();
        }
    }
}
