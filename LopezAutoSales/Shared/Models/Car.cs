﻿using System;
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
        public string VIN { get { return vin; } set { vin = value.ToUpper(); } }
        private string vin;
        [Required]
        public int Year { get; set; }
        [Required]
        public string Make { get { return make; } set { make = value.ToTitleCase(); } }
        private string make;
        [Required]
        public string Model { get { return model; } set { model = value.ToTitleCase(); } }
        private string model;
        [Required]
        public string Color { get { return color; } set { color = value.ToTitleCase(); } }
        private string color;
        public int? Mileage { get; set; }
        public bool IsSalvage { get; set; }
        public bool IsListed { get; set; }
        [Column(TypeName = "decimal(9,2)")]
        public decimal? BoughtPrice { get; set; }
        [Column(TypeName = "decimal(9,2)")]
        public decimal ListPrice { get; set; }
        public List<Picture> Pictures { get; set; } = new List<Picture>();
        public string JsonData { get; set; }
        [NotMapped]
        public CarData Data { get; set; }

        public string Name()
        {
            return $"{Year} {Make} {Model}";
        }

        public void DeserializeJson()
        {
            if (!string.IsNullOrEmpty(JsonData))
                Data = JsonSerializer.Deserialize<CarData>(JsonData);
        }

        public void Update(Car car)
        {
            Year = car.Year;
            Make = car.Make;
            Model = car.Model;
            Mileage = car.Mileage;
            Color = car.Color;
            BoughtPrice = car.BoughtPrice;
            VIN = car.VIN;
        }

        public string MileageString()
        {
            return Mileage.HasValue ? Mileage.Value.ToString("N0") : "Exempt";
        }

        public string TitleStatus()
        {
            return IsSalvage ? "Rebuilt Salvage" : "Clean";
        }
    }
}
