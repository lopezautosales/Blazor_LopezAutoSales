using System.ComponentModel.DataAnnotations;

namespace LopezAutoSales.Shared.Models
{
    public class Address
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Street { get; set; }
        [Required]
        [MaxLength(20)]
        public string City { get; set; }
        [Required]
        [MaxLength(20)]
        public string State { get; set; }
        [Required]
        [MaxLength(10)]
        public string ZIP { get; set; }
    }
}
