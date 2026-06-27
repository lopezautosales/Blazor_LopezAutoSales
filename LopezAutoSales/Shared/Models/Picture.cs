namespace LopezAutoSales.Shared.Models
{
    public class Picture
    {
        public int Id { get; set; }

        public int CarId { get; set; }
        public Car Car { get; set; }
        public string URL { get; set; }
        // Marks the car's cover photo (shown first in the grid/carousel). Display sizes
        // are produced on the fly by Cloudflare resizing; there is no separate thumbnail file.
        public bool IsThumbnail { get; set; }
    }
}
