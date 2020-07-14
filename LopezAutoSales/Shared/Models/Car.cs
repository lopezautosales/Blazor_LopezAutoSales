using System;
using System.Collections.Generic;

namespace LopezAutoSales.Shared.Models
{
    public class Car
    {
        public DateTime Date { get; set; }
        public string VIN { get; set; }

        public int Year { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string Color { get; set; }
        public int? Mileage { get; set; }
        public bool IsSalvage { get; set; }
        public List<Image> Images { get; set; } = new List<Image>();
    }
}
