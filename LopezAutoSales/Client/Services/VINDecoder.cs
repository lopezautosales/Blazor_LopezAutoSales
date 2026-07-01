using LopezAutoSales.Shared.Models;
using Microsoft.JSInterop;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LopezAutoSales.Client
{
    public class VINDecoder
    {
        public const string Path = "https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVin/{0}?format=json";
        private readonly HttpClient _client;
        private readonly IJSRuntime _js;

        public string DecodedVIN { get; set; }
        public string JsonData { get; set; }

        public VINDecoder(HttpClient client, IJSRuntime js)
        {
            _client = client;
            _js = js;
        }

        public async Task TryDecodeAsync(Car car)
        {
            if (DecodedVIN != car.VIN)
            {
                try
                {
                    JsonData = await _client.GetStringAsync(string.Format(Path, car.VIN));
                }
                catch (Exception)
                {
                    // NHTSA is a best-effort third party. If it's unreachable we must NOT
                    // throw — an unhandled exception here bubbles to the ErrorBoundary and
                    // wipes the in-progress vehicle/sale entry. Let the user type details in.
                    await _js.InvokeVoidAsync("alert", "The VIN decoder is unavailable right now — enter the vehicle details manually.");
                    return;
                }
            }
            else if (car.JsonData == JsonData)
                return;

            car.JsonData = JsonData;
            if (!TrySetVariables(car))
                await _js.InvokeVoidAsync("alert", $"{car.VIN} could not be decoded.");
            else if (DecodedVIN != car.VIN)
                await _js.InvokeVoidAsync("alert", $"{car.VIN} was successfully decoded.");
            DecodedVIN = car.VIN;
        }

        private bool TrySetVariables(Car car)
        {
            // Defensive throughout: malformed NHTSA JSON (null Data, missing variables,
            // blank year) must not throw — like the HTTP failure above, an exception here
            // reaches the ErrorBoundary and wipes the in-progress vehicle/sale form.
            try
            {
                car.DeserializeJson();
                var results = car.Data?.Results;
                if (results == null)
                    return false;
                if (results.Find(x => x.VariableId == 143)?.Value != "0") // 143 = error code
                    return false;
                if (!int.TryParse(results.Find(x => x.VariableId == 29)?.Value, out int year))
                    return false;
                string make = results.Find(x => x.VariableId == 26)?.Value;
                string model = results.Find(x => x.VariableId == 28)?.Value;
                if (string.IsNullOrEmpty(make) || string.IsNullOrEmpty(model))
                    return false;
                car.Year = year;
                car.Make = make;
                car.Model = model;
                return true;
            }
            catch
            {
                return false; // caller alerts "could not be decoded" — manual entry continues
            }
        }
    }
}
