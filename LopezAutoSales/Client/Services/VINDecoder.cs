using LopezAutoSales.Shared.Models;
using System.Net.Http;
using System.Threading.Tasks;

namespace LopezAutoSales.Client
{
    public class VINDecoder
    {
        public const string Path = "https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVin/{0}?format=json";
        private readonly HttpClient _client;

        private string DecodedVIN { get; set; }
        private string JsonData { get; set; }

        public VINDecoder(HttpClient client)
        {
            _client = client;
        }

        public async Task<bool> TryDecodeAsync(Car car)
        {
            if (DecodedVIN != car.VIN)
            {
                JsonData = await _client.GetStringAsync(string.Format(Path, car.VIN));
            }
            DecodedVIN = car.VIN;
            car.JsonData = JsonData;
            return SetVariables(car);
        }

        private bool SetVariables(Car car)
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
