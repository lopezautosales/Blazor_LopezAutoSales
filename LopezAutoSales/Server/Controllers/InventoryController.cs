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
            // Guard against listing the same physical car twice — SellVehicle matches by
            // VIN among listed cars, so a duplicate listing corrupts the sell flow + reports.
            if (!string.IsNullOrWhiteSpace(car.VIN) && _context.Cars.Any(x => x.IsListed && x.VIN.ToUpper() == car.VIN.ToUpper()))
                return BadRequest(new[] { $"A listed vehicle with VIN {car.VIN} is already in inventory." });
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
                return NotFound("Car was not found.");
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
                return NotFound("Picture was not found.");

            Picture otherPicture = null;
            if (picture.IsThumbnail)
            {
                otherPicture = _context.Pictures.FirstOrDefault(x => x.Id != id && x.CarId == picture.CarId);
                if (otherPicture != null)
                {
                    otherPicture.IsThumbnail = true;
                    newThumbnailId = otherPicture.Id;
                }
            }
            _context.Remove(picture);
            _context.SaveChanges();

            // Storage cleanup after the DB commit — orphaned blobs are tolerated, but
            // a failed SaveChanges must not leave records pointing at deleted blobs.
            await _storage.DeleteAsync(picture.URL);
            return Ok(newThumbnailId);
        }

        [HttpPut("thumbnail/{carId}")]
        [Authorize(Roles = "Admin")]
        public IActionResult SetThumbnail([FromRoute] int carId, [FromBody] int pictureId)
        {
            Car car = _context.Cars.Include(x => x.Pictures).FirstOrDefault(x => x.Id == carId);
            if (car == null)
                return NotFound();

            Picture picture = car.Pictures.FirstOrDefault(x => x.Id == pictureId);
            if (picture == null)
                return NotFound();
            // IsThumbnail is just the cover-photo flag now; no thumbnail file to regenerate.
            foreach (Picture removable in car.Pictures.Where(x => x.IsThumbnail))
                removable.IsThumbnail = false;
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
                return NotFound("Car not found.");
            (List<Picture> added, int skipped) = await HandleImagesAsync(car);
            _context.SaveChanges();
            ResolveUrls(added);
            // Tell the client if any files were rejected (not an image / too large / corrupt)
            // so a partial upload doesn't look like a clean success.
            if (skipped > 0)
                Response.Headers["X-Skipped-Files"] = skipped.ToString();
            return Ok(added);
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
            List<Picture> pictures = car.Pictures.ToList();
            _context.Remove(car);
            _context.SaveChanges();

            // Remove blobs after the DB commit so a failed save can't orphan records.
            foreach (Picture picture in pictures)
                await _storage.DeleteAsync(picture.URL);
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

        // Returns the pictures actually stored and the count of files that were skipped
        // (undecodable, oversized, or failed mid-processing). One bad file never aborts
        // the rest of the batch.
        private async Task<(List<Picture> Added, int Skipped)> HandleImagesAsync(Car car)
        {
            List<Picture> pictures = new List<Picture>();
            bool hasThumbnail = car.Pictures.Any(x => x.IsThumbnail);
            int skipped = 0;
            foreach (var file in HttpContext.Request.Form.Files)
            {
                try
                {
                    // Buffer so we can inspect the header before decoding the full image.
                    using MemoryStream buffer = new MemoryStream();
                    await file.CopyToAsync(buffer);
                    buffer.Position = 0;

                    IImageInfo info = Image.Identify(buffer);
                    buffer.Position = 0;
                    if (info == null || (long)info.Width * info.Height > MaxPixels)
                    {
                        skipped++;
                        _logger.LogWarning("Skipped upload {File}: not a decodable image or exceeds the pixel limit.", file.FileName);
                        continue;
                    }

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

                    // Store the blob first; only track the record once the blob exists.
                    await SaveImageAsync(image, format, picture.URL);
                    pictures.Add(picture);
                    // The car's first picture becomes its cover (IsThumbnail). Display sizes
                    // are produced on the fly by Cloudflare resizing — no thumbnail file.
                    if (!hasThumbnail)
                    {
                        picture.IsThumbnail = true;
                        hasThumbnail = true;
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    _logger.LogError(ex, "Skipped upload {File}: processing failed.", file.FileName);
                }
            }
            _context.Pictures.AddRange(pictures);
            return (pictures, skipped);
        }

        private async Task SaveImageAsync(Image image, IImageFormat format, string key)
        {
            IImageEncoder encoder = SixLabors.ImageSharp.Configuration.Default.ImageFormatsManager.FindEncoder(format);
            using MemoryStream ms = new MemoryStream();
            image.Save(ms, encoder);
            ms.Position = 0;
            await _storage.SaveAsync(key, ms, format.DefaultMimeType);
        }

        #endregion Helpers
    }
}