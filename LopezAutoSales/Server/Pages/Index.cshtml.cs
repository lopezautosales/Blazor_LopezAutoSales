using LopezAutoSales.Server.Models;
using LopezAutoSales.Server.Storage;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Pages
{
    public class IndexModel : PageModel
    {
        private const int PageSize = 12;
        private readonly ApplicationDbContext _context;
        private readonly IImageStorage _storage;

        public List<CarCard> Cars { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

        public IndexModel(ApplicationDbContext context, IImageStorage storage)
        {
            _context = context;
            _storage = storage;
        }

        // 'p', not 'page' — 'page' is a reserved Razor Pages route token and won't bind.
        public async Task<IActionResult> OnGetAsync(int p = 1)
        {
            IQueryable<Car> query = _context.Cars.AsNoTracking().Where(c => c.IsListed).OrderByDescending(c => c.ListPrice);
            int total = await query.CountAsync();
            TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            CurrentPage = Math.Min(Math.Max(p, 1), TotalPages);

            var rows = await query
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .Select(c => new
                {
                    c.Id,
                    c.Year,
                    c.Make,
                    c.Model,
                    c.ListPrice,
                    c.Mileage,
                    ThumbnailKey = c.Pictures.Where(p => p.IsThumbnail).Select(p => p.URL).FirstOrDefault()
                })
                .ToListAsync();

            Cars = rows.Select(r => new CarCard
            {
                Id = r.Id,
                Name = $"{r.Year} {r.Make} {r.Model}",
                ListPrice = r.ListPrice,
                Mileage = r.Mileage,
                ThumbnailUrl = r.ThumbnailKey == null ? null : _storage.PublicUrl(new Picture { URL = r.ThumbnailKey }.ThumbnailURL())
            }).ToList();

            return Page();
        }
    }
}
