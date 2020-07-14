namespace LopezAutoSales.Shared.Models
{
    public class Lienholder
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int AddressId { get; set; }
        public Address Address { get; set; }
    }
}
