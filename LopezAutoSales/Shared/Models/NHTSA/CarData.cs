using System.Collections.Generic;

namespace LopezAutoSales.Shared.Models
{
    public class CarData
    {
        public int Count { get; set; }
        public string Message { get; set; }
        public string SearchCriteria { get; set; }
        public List<CarProperty> Results { get; set; }
    }
}
