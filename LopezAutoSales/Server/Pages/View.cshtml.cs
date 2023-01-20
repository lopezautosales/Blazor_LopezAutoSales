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
        public Car Car { get; set; }

        public ViewModel(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult OnGet(int id)
        {
            Car = _context.Cars.AsNoTracking().Include(x => x.Pictures).FirstOrDefault(x => x.Id == id);
            Car.DeserializeJson();
            return Page();
        }
    }
}