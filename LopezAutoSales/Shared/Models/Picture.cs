namespace LopezAutoSales.Shared.Models
{
    public class Picture
    {
        public int Id { get; set; }

        public int CarId { get; set; }
        public Car Car { get; set; }
        public string URL { get; set; }
        public bool IsThumbnail { get; set; }
    }
}
