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

        public override string ToString()
        {
            return $"{Street}, {City}, {State}, {ZIP}";
        }

        public string Area()
        {
            return $"{City}, {State}, {ZIP}";
        }

        public void Update(Address address)
        {
            Street = address.Street;
            City = address.City;
            State = address.State;
            ZIP = address.ZIP;
        }
    }
}
