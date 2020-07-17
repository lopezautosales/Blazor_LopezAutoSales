using LopezAutoSales.Server.Data;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.AspNetCore.Hosting;

namespace LopezAutoSales.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public InventoryController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        [HttpGet]
        public async Task<IActionResult> GetInventory()
        {
            List<Car> cars = await _context.Cars.AsNoTracking().Where(x => x.IsListed).Include(x => x.Pictures).ToListAsync();
            cars.ForEach(x => x.BoughtPrice = null);
            return Ok(cars);
        }

        [HttpGet]
        public async Task<IActionResult> GetWarranty(string id)
        {
            Car car = await _context.Cars.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            car.BoughtPrice = null;
            return Ok(car);
        }

        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminInventory()
        {
            List<Car> cars = await _context.Cars.AsNoTracking().Where(x => x.IsListed).Include(x => x.Pictures).ToListAsync();
            return Ok(cars);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddVehicle(CarUpload data)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrors());

            data.Files.ForEach(x =>
            {
                if (x.Contains(','))
                    x = x.Split(',')[1];
            });
            for(int i = 0; i < data.Files.Count; i++)
            {
                var file = data.Files[i];
                //create path
                string path = $"/Images/{data.Car.VIN}_{Path.GetRandomFileName()}.jpg";
                byte[] bytes = Convert.FromBase64String(file);

                //create thumbnail
                if (data.ThumbanilIndex == i)
                {
                    string thumbPath = path.Replace(".jpg", ".thumbnail.jpg");
                    data.Car.Pictures.Add(new Picture
                    {
                        IsThumbnail = true,
                        URL = thumbPath
                    });
                    using Image thumbnail = Image.Load(bytes);
                    thumbnail.Mutate(x => x.AutoOrient().Resize(200, 200));
                    CreateImage(thumbPath, thumbnail);
                }
                //add
                data.Car.Pictures.Add(new Picture
                {
                    IsThumbnail = false,
                    URL = path
                });

                //save
                using Image image = Image.Load(bytes);
                image.Mutate(x => x.AutoOrient());
                CreateImage(path, image);
            }

            data.Car.IsListed = true;
            data.Car.Date = DateTime.Now;
            _context.Cars.Add(data.Car);
            _context.SaveChanges();
            return Ok(data.Car.Id);
        }

        private void CreateImage(string path, Image image)
        {
            using var fileStream = new FileStream(_hostEnvironment.WebRootPath + path, FileMode.OpenOrCreate);
            image.Save(fileStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
        }
    }
}
