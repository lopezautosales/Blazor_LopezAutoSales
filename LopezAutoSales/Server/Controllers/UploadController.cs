using LopezAutoSales.Server.Data;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
                Picture picture = new Picture
                {
                    CarId = car.Id,
                    IsThumbnail = false,
                    URL = Path.Combine("Images", file.FileName)
                };
                pictures.Add(picture);
                using Image image = Image.Load(file.OpenReadStream());
                image.Mutate(x => x.AutoOrient());
                image.Save(FullPath(picture.URL));
                if (!hasThumbnail)
                {
                    CreateThumbnail(picture);
                    picture.IsThumbnail = true;
                    hasThumbnail = true;
                }
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
            CreateThumbnail(picture);
            List<Picture> thumbnails = car.Pictures.Where(x => x.IsThumbnail).ToList();
            foreach (Picture removable in thumbnails)
            {
                removable.IsThumbnail = false;
                System.IO.File.Delete(FullPath(removable.ThumbnailURL()));
            }
            picture.IsThumbnail = true;
            _context.SaveChanges();
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemovePicture(int id)
        {
            Picture picture = await _context.Pictures.FindAsync(id);
            if (picture == null)
                return BadRequest();
            System.IO.File.Delete(FullPath(picture.URL));
            if (picture.IsThumbnail)
                System.IO.File.Delete(FullPath(picture.ThumbnailURL()));
            _context.Remove(picture);
            _context.SaveChanges();
            return Ok();
        }

        private void CreateThumbnail(Picture picture)
        {
            using Image image = Image.Load(FullPath(picture.URL));
            double ratio = Constants.ThumbnailSize / (double)image.Width;
            image.Mutate(x => x.AutoOrient().Resize((int)(image.Width * ratio), (int)(image.Height * ratio)));
            image.Save(FullPath(picture.ThumbnailURL()));
        }

        private string FullPath(string path)
        {
            return Path.Combine(_hostEnvironment.WebRootPath, path);
        }
    }
}
