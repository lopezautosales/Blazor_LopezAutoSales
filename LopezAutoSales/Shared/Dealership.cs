using LopezAutoSales.Shared.Models;

namespace LopezAutoSales.Shared
{
    public static class Dealership
    {
        public const string Name = "Lopez Auto Sales, Inc.";
        public static Address Address = new Address
        {
            Street = "710 Lantern Lane",
            City = "Emporia",
            State = "Kansas",
            ZIP = "66801"
        };
        public const string Email = "lopezauto@outlook.com";
        public const string Phone = "(620)208-6250";
        public const int Warranty = 20;
        public const decimal TaxRate = 8.5m;
    }
}
