namespace LopezAutoSales.Server.Models
{
    // Lightweight projection for the public inventory grid — avoids loading JsonData
    // and non-thumbnail pictures for every listed car.
    public class CarCard
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal ListPrice { get; set; }
        public int? Mileage { get; set; }
        public string ThumbnailUrl { get; set; }
    }
}
