namespace LopezAutoSales.Shared.Models
{
    public class Image
    {
        public string Id { get; set; }

        public Car Car { get; set; }
        public string URL { get; set; }
        public bool IsThumbnail { get; set; }
    }
}
