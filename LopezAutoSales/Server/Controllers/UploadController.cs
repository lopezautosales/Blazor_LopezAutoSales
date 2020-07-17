using LopezAutoSales.Server.Data;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace LopezAutoSales.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public UploadController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        [HttpPost("car/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadImage([FromRoute] int id)
        {
            Car car = await _context.Cars.AsNoTracking().Where(x => x.Id == id).Include(x => x.Pictures).FirstOrDefaultAsync();
            if (car == null)
                return BadRequest();
            if (!HttpContext.Request.Form.Files.Any())
                return BadRequest();
            List<Picture> pictures = new List<Picture>();
            bool hasThumbnail = car.Pictures.Any(x => x.IsThumbnail);
            foreach (var file in HttpContext.Request.Form.Files)
            {
                var path = Path.Combine(_hostEnvironment.ContentRootPath, "Images", file.FileName);
                pictures.Add(new Picture
                {
                    CarId = car.Id,
                    IsThumbnail = false,
                    URL = path
                });
                if (!hasThumbnail)
                {
                    string thumbnailPath = path.Insert(path.LastIndexOf('.'), "thumbnail.");
                    pictures.Add(new Picture
                    {
                        CarId = car.Id,
                        IsThumbnail = true,
                        URL = thumbnailPath
                    });
                    using Image thumbnailImage = Image.Load(file.OpenReadStream());
                    thumbnailImage.Mutate(x => x.AutoOrient().Resize(200, 200));
                    thumbnailImage.Save(thumbnailPath);
                    hasThumbnail = true;
                }
                using Image image = Image.Load(file.OpenReadStream());
                image.Mutate(x => x.AutoOrient());
                image.Save(path);
            }
            _context.Pictures.AddRange(pictures);
            _context.SaveChanges();
            return Ok(pictures);
        }

        [HttpPut("thumbnail/{carId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetThumbnail([FromRoute] int carId, [FromBody] int pictureId)
        {
            Car car = await _context.Cars.Where(x => x.Id == carId).Include(x => x.Pictures).FirstOrDefaultAsync();
            if (car == null)
                return BadRequest();

            Picture picture = car.Pictures.FirstOrDefault(x => x.Id == pictureId);
            if (picture == null || picture.IsThumbnail)
                return BadRequest();
            string path = picture.URL.Insert(picture.URL.LastIndexOf('.'), ".thumbnail");
            using Image image = Image.Load(picture.URL);
            image.Mutate(x => x.Resize(200, 200));
            image.Save(path);
            List<Picture> thumbnails = car.Pictures.Where(x => x.IsThumbnail).ToList();
            if(thumbnails.Count > 0)
            {
                foreach (Picture removable in thumbnails)
                    System.IO.File.Delete(removable.URL);
                _context.Pictures.RemoveRange(thumbnails);
            }
            Picture thumbnail = new Picture
            {
                CarId = car.Id,
                IsThumbnail = true,
                URL = path
            };
            _context.Add(thumbnail);
            _context.SaveChanges();
            return Ok();
        }
    }
}
