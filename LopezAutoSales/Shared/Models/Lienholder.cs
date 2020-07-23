using System.ComponentModel.DataAnnotations;

namespace LopezAutoSales.Shared.Models
{
    public class Lienholder
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public int AddressId { get; set; }
        [Required]
        public Address Address { get; set; }
    }
}
