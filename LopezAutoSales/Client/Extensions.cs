using LopezAutoSales.Shared.Models;

namespace LopezAutoSales.Client
{
    public static class Extensions
    {
        public static string MileageString(this Car car)
        {
            return car.Mileage.HasValue ? car.Mileage.Value.ToString("N0") : "Exempt";
        }

        public static string TitleStatus(this Car car)
        {
            return car.IsSalvage ? "Rebuilt Salvage" : "Clean";
        }
    }
}
