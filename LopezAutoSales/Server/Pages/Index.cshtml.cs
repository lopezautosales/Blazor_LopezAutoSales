using LopezAutoSales.Server.Storage;
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
        private readonly IImageStorage _storage;

        public List<Car> Cars { get; set; }

        public IndexModel(ApplicationDbContext context, IImageStorage storage)
        {
            _context = context;
            _storage = storage;
        }

        public IActionResult OnGet()
        {
            Cars = _context.Cars.AsNoTracking().Where(x => x.IsListed).OrderByDescending(x => x.ListPrice).Include(x => x.Pictures).ToList();
            foreach (Car car in Cars)
                foreach (Picture picture in car.Pictures)
                    picture.URL = _storage.PublicUrl(picture.URL);
            return Page();
        }
    }
}
