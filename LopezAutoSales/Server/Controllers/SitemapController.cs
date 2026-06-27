using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;

namespace LopezAutoSales.Server.Controllers
{
    [ApiController]
    public class SitemapController : ControllerBase
    {
        private const string BaseUrl = "https://lopezautosales.com";
        private readonly ApplicationDbContext _context;

        public SitemapController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("/sitemap.xml")]
        public IActionResult Sitemap()
        {
            var ids = _context.Cars.AsNoTracking().Where(c => c.IsListed).Select(c => c.Id).ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            sb.AppendLine($"  <url><loc>{BaseUrl}/</loc></url>");
            sb.AppendLine($"  <url><loc>{BaseUrl}/about</loc></url>");
            foreach (int id in ids)
                sb.AppendLine($"  <url><loc>{BaseUrl}/view/{id}</loc></url>");
            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml");
        }
    }
}
