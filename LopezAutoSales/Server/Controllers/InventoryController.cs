using IdentityServer4.Extensions;
using LopezAutoSales.Server.Data;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LopezAutoSales.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InventoryController> _logger;
        private readonly IWebHostEnvironment _env;

        public InventoryController(ApplicationDbContext context, ILogger<InventoryController> logger, IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
        }

        [HttpGet]
        public IActionResult GetInventory()
        {
            List<Car> cars = _context.Cars.AsNoTracking().Where(x => x.IsListed).Include(x => x.Pictures).ToList();
            if (!User.IsInRole("Admin"))
                cars.ForEach(x => x.BoughtPrice = null);
            return Ok(cars);
        }

        [HttpGet("{id}")]
        public IActionResult GetCar(int id)
        {
            Car car = _context.Cars.AsNoTracking().Include(x => x.Pictures).FirstOrDefault(x => x.Id == id);
            if (car == null)
                return BadRequest();
            if (!User.IsInRole("Admin"))
                car.BoughtPrice = null;
            return Ok(car);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddVehicle(Car car)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());
            _logger.LogInformation($"{User.GetDisplayName()} ADDED {car.Name()} FOR {car.ListPrice}");
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
                return BadRequest();
            _logger.LogInformation($"{User.GetDisplayName()} EDITED {car.Name()} FOR {car.ListPrice}");
            car.Update(data);
            car.IsSalvage = data.IsSalvage;
            car.JsonData = data.JsonData;
            car.ListPrice = data.ListPrice;
            _context.SaveChanges();
            return Ok();
        }

        [HttpDelete("picture/{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult RemovePicture(int id)
        {
            Picture picture = _context.Pictures.Find(id);
            if (picture == null)
                return BadRequest();
            System.IO.File.Delete(FullPath(picture.URL));
            if (picture.IsThumbnail)
                System.IO.File.Delete(FullPath(picture.ThumbnailURL()));
            _context.Remove(picture);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPut("thumbnail/{carId}")]
        [Authorize(Roles = "Admin")]
        public IActionResult SetThumbnail([FromRoute] int carId, [FromBody] int pictureId)
        {
            Car car = _context.Cars.Include(x => x.Pictures).FirstOrDefault(x => x.Id == carId);
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

        [HttpPost("upload/{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult AddPictures(int id)
        {
            if (!HttpContext.Request.Form.Files.Any())
                return BadRequest("No images uploaded.");
            Car car = _context.Cars.Include(x => x.Pictures).FirstOrDefault(x => x.Id == id);
            if (car == null)
                return BadRequest("Car not found.");
            HandleImages(car);
            _context.SaveChanges();
            return Ok(car.Pictures);
        }

        public void HandleImages(Car car)
        {
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
            return Path.Combine(_env.WebRootPath, path);
        }
    }
}
