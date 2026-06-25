using LopezAutoSales.Server.Storage;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace LopezAutoSales.Server.Pages
{
    public class ViewModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IImageStorage _storage;
        public Car Car { get; set; }

        public ViewModel(ApplicationDbContext context, IImageStorage storage)
        {
            _context = context;
            _storage = storage;
        }
        public IActionResult OnGet(int id)
        {
            Car = _context.Cars.AsNoTracking().Include(x => x.Pictures).FirstOrDefault(x => x.Id == id && x.IsListed);
            if (Car == null)
                return NotFound();
            Car.DeserializeJson();
            foreach (Picture picture in Car.Pictures)
                picture.URL = _storage.PublicUrl(picture.URL);
            return Page();
        }
    }
}