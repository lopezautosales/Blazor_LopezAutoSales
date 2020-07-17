using System.Collections.Generic;

namespace LopezAutoSales.Shared.Models
{
    public class CarUpload
    {
        public Car Car { get; set; } = new Car();
        public List<string> Files { get; set; }
        public int ThumbanilIndex { get; set; }
    }
}
