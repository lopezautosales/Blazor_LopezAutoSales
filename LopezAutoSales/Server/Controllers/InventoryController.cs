using LopezAutoSales.Server.Storage;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InventoryController> _logger;
        private readonly IImageStorage _storage;

        public InventoryController(ApplicationDbContext context, ILogger<InventoryController> logger, IImageStorage storage)
        {
            _context = context;
            _logger = logger;
            _storage = storage;
        }

        [HttpGet]
        public IActionResult GetInventory()
        {
            List<Car> cars = _context.Cars.AsNoTracking().Where(x => x.IsListed).Include(x => x.Pictures).ToList();
            if (!User.IsInRole("Admin"))
                cars.ForEach(x => x.BoughtPrice = null);
            cars.ForEach(x => ResolveUrls(x.Pictures));
            return Ok(cars);
        }

        [HttpGet("{id}")]
        public IActionResult GetCar(int id)
        {
            Car car = _context.Cars.AsNoTracking().Include(x => x.Pictures).FirstOrDefault(x => x.Id == id);
            // Non-admins may only view listed cars (no id-enumeration of sold/unlisted).
            if (car == null || (!car.IsListed && !User.IsInRole("Admin")))
                return NotFound();
            if (!User.IsInRole("Admin"))
                car.BoughtPrice = null;
            ResolveUrls(car.Pictures);
            return Ok(car);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddVehicle(Car car)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
            _logger.LogInformation($"{User.Identity?.Name} ADDED {car.Name()} FOR {car.ListPrice}");
            car.IsListed = true;
            car.Date = DateTime.Now;
            _context.Cars.Add(car);
            _context.SaveChanges();
            return Ok(car.Id);
        }

        [HttpPut("edit/{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult EditVehicle([FromRoute] int id, [FromBody] Car data)
        {
            Car car = _context.Cars.FirstOrDefault(x => x.Id == id);
            if (car == null)
                return BadRequest("Car was not found.");
            _logger.LogInformation($"{User.Identity?.Name} EDITED {car.Name()} FOR {car.ListPrice}");
            car.Update(data);
            car.IsSalvage = data.IsSalvage;
            car.JsonData = data.JsonData;
            car.ListPrice = data.ListPrice;
            _context.SaveChanges();
            return Ok();
        }

        [HttpDelete("picture/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemovePicture(int id)
        {
            int newThumbnailId = 0;
            Picture picture = _context.Pictures.Find(id);
            if (picture == null)
                return BadRequest("Picture was not found.");
            await _storage.DeleteAsync(picture.URL);
            if (picture.IsThumbnail)
            {
                await _storage.DeleteAsync(picture.ThumbnailURL());

                Picture otherPicture = _context.Pictures.FirstOrDefault(x => x.Id != id && x.CarId == picture.CarId);
                if (otherPicture != null)
                {
                    await CreateThumbnailAsync(otherPicture);
                    otherPicture.IsThumbnail = true;
                    newThumbnailId = otherPicture.Id;
                }
            }
            _context.Remove(picture);
            _context.SaveChanges();
            return Ok(newThumbnailId);
        }

        [HttpPut("thumbnail/{carId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetThumbnail([FromRoute] int carId, [FromBody] int pictureId)
        {
            Car car = _context.Cars.Include(x => x.Pictures).FirstOrDefault(x => x.Id == carId);
            if (car == null)
                return BadRequest();

            Picture picture = car.Pictures.FirstOrDefault(x => x.Id == pictureId);
            if (picture == null)
                return BadRequest();
            List<Picture> thumbnails = car.Pictures.Where(x => x.IsThumbnail).ToList();
            foreach (Picture removable in thumbnails)
            {
                removable.IsThumbnail = false;
                await _storage.DeleteAsync(removable.ThumbnailURL());
            }
            await CreateThumbnailAsync(picture);
            picture.IsThumbnail = true;
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost("upload/{id}")]
        [Authorize(Roles = "Admin")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> AddPictures(int id)
        {
            if (!HttpContext.Request.Form.Files.Any())
                return BadRequest("No images uploaded.");
            Car car = _context.Cars.Include(x => x.Pictures).FirstOrDefault(x => x.Id == id);
            if (car == null)
                return BadRequest("Car not found.");
            await HandleImagesAsync(car);
            _context.SaveChanges();
            ResolveUrls(car.Pictures);
            return Ok(car.Pictures);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            Car car = _context.Cars.Include(x => x.Pictures).FirstOrDefault(x => x.Id == id);

            if (car == null)
                return NotFound("Car was not found.");
            if (!car.IsListed)
                return BadRequest("Cannot remove cars that are not listed.");

            _logger.LogInformation($"{User.Identity?.Name} DELETED {car.Name()}");
            // Remove the car's blobs from object storage so they don't leak.
            foreach (Picture picture in car.Pictures)
            {
                await _storage.DeleteAsync(picture.URL);
                if (picture.IsThumbnail)
                    await _storage.DeleteAsync(picture.ThumbnailURL());
            }
            _context.Remove(car);
            _context.SaveChanges();
            return Ok();
        }

        #region Helpers

        // Rewrites stored object keys to absolute public URLs for display. Safe to call
        // on materialized entities once they are no longer going to be saved.
        private void ResolveUrls(IEnumerable<Picture> pictures)
        {
            foreach (Picture picture in pictures)
                picture.URL = _storage.PublicUrl(picture.URL);
        }

        // Reject images larger than this many pixels (decompression-bomb guard).
        private const long MaxPixels = 100_000_000;

        private async Task HandleImagesAsync(Car car)
        {
            List<Picture> pictures = new List<Picture>();
            bool hasThumbnail = car.Pictures.Any(x => x.IsThumbnail);
            foreach (var file in HttpContext.Request.Form.Files)
            {
                // Buffer so we can inspect the header before decoding the full image.
                using MemoryStream buffer = new MemoryStream();
                await file.CopyToAsync(buffer);
                buffer.Position = 0;

                IImageInfo info = Image.Identify(buffer);
                buffer.Position = 0;
                if (info == null || (long)info.Width * info.Height > MaxPixels)
                    continue; // not a decodable image, or too large — skip

                using Image image = Image.Load(buffer, out IImageFormat format);
                image.Mutate(x => x.AutoOrient());

                // Key from a GUID + the *detected* format (never the client filename),
                // so uploads can't collide/overwrite or smuggle a wrong extension.
                string extension = format.FileExtensions.FirstOrDefault() ?? "img";
                Picture picture = new Picture
                {
                    CarId = car.Id,
                    IsThumbnail = false,
                    URL = $"Images/{Guid.NewGuid():N}.{extension}"
                };
                pictures.Add(picture);

                await SaveImageAsync(image, format, picture.URL);
                if (!hasThumbnail)
                {
                    await CreateThumbnailAsync(picture);
                    picture.IsThumbnail = true;
                    hasThumbnail = true;
                }
            }
            _context.Pictures.AddRange(pictures);
        }

        private async Task CreateThumbnailAsync(Picture picture)
        {
            try
            {
                using Stream original = await _storage.OpenReadAsync(picture.URL);
                using Image image = Image.Load(original, out IImageFormat format);
                double ratio = Constants.ThumbnailSize / (double)image.Width;
                image.Mutate(x => x.AutoOrient().Resize((int)(image.Width * ratio), (int)(image.Height * ratio)));
                await SaveImageAsync(image, format, picture.ThumbnailURL());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        private async Task SaveImageAsync(Image image, IImageFormat format, string key)
        {
            IImageEncoder encoder = Configuration.Default.ImageFormatsManager.FindEncoder(format);
            using MemoryStream ms = new MemoryStream();
            image.Save(ms, encoder);
            ms.Position = 0;
            await _storage.SaveAsync(key, ms, format.DefaultMimeType);
        }

        #endregion Helpers
    }
}