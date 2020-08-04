using LopezAutoSales.Shared.Models;

namespace LopezAutoSales.Shared
{
    public static class Dealership
    {
        public const string Name = "Lopez Auto Sales, Inc.";
        public static Address Address = new Address
        {
            Street = "515 Albert St",
            City = "Emporia",
            State = "Kansas",
            ZIP = "66801"
        };
        public const string Email = "lopezauto@outlook.com";
        public const string Phone = "(620)208-6250";
        public const int Warranty = 20;
        public const decimal TaxRate = 8.5m;
        public const int MonthlyPayment = 300;
        public const decimal TagAmount = 10;
        public const decimal LienAmount = 20;
        public const decimal APR = 0;
        public const int LateDays = 15;
        public const decimal LateFee = 15m;
        public const decimal LateRate = 5m;
        public const decimal LateAPR = 10;
    }
}
