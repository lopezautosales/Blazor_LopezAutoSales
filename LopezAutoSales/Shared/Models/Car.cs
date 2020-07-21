using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace LopezAutoSales.Shared.Models
{
    public class Car
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }
        [Required]
        public string VIN { get; set; }
        [Required]
        public int Year { get; set; }
        [Required]
        public string Make { get; set; }
        [Required]
        public string Model { get; set; }
        [Required]
        public string Color { get; set; }
        public int? Mileage { get; set; }
        public bool IsSalvage { get; set; }
        public bool IsListed { get; set; }
        [Column(TypeName = "money")]
        public decimal? BoughtPrice { get; set; }
        [Column(TypeName = "money")]
        public decimal ListPrice { get; set; }
        public List<Picture> Pictures { get; set; } = new List<Picture>();
        public string JsonData { get; set; }
        [NotMapped]
        public CarData Data { get; set; }

        public string Name
        {
            get
            {
                return $"{Year} {Make} {Model}";
            }
        }

        public void DeserializeJson()
        {
            Data = JsonSerializer.Deserialize<CarData>(JsonData);
        }
    }
}
