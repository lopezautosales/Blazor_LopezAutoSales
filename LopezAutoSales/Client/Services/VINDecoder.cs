using LopezAutoSales.Shared.Models;
using System.Net.Http;
using System.Threading.Tasks;

namespace LopezAutoSales.Client
{
    public static class VINDecoder
    {
        public const string Path = "https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVin/{0}?format=json";

        public static async Task<string> Decode(this HttpClient client, string vin)
        {
            return await client.GetStringAsync(string.Format(Path, vin));
        }

        public static bool TrySetVariables(this Car car)
        {
            car.DeserializeJson();
            if (car.Data.Results.Find(x => x.VariableId == 143).Value != "0")
                return false;
            car.Year = int.Parse(car.Data.Results.Find(x => x.VariableId == 29).Value);
            car.Make = car.Data.Results.Find(x => x.VariableId == 26).Value;
            car.Model = car.Data.Results.Find(x => x.VariableId == 28).Value;
            return true;
        }
    }
}
