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
        // Customer-facing hours. Shown in the footer + About; mirrored as Mon–Sat
        // 10:00–17:00 in the AutoDealer openingHoursSpecification JSON-LD (_Layout.cshtml).
        public const string Hours = "Mon–Sat: 10 AM – 5 PM";
        public const string HoursNote = "Sundays & after-hours by appointment";
        public const int Warranty = 20;
        public const decimal TaxRate = 8.5m;
        public const decimal MonthlyPayment = 300m;
        public const decimal TagAmount = 20;
        public const decimal LienAmount = 20;
        public const decimal APR = 0;
        public const int LateDays = 15;
        public const decimal LateFee = 15m;
        public const decimal LateRate = 5m;
        public const decimal LateAPR = 10;
        public const decimal PaperworkFee = 125m;
    }
}
