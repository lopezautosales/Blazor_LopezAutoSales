using System.ComponentModel.DataAnnotations;

namespace LopezAutoSales.Shared.Models
{
    public class Lienholder
    {
        [Required]
        public string Name { get; set; } = Dealership.Name;
        [Key]
        public string NormalizedName { get; set; } = Dealership.Name.ToUpper();
        public int AddressId { get; set; }
        [Required]
        public Address Address { get; set; }

        public void Update(Lienholder lienholder)
        {
            Address.Update(lienholder.Address);
        }
    }
}
