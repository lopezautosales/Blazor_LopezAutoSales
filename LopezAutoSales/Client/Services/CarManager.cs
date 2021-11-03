using Blazored.SessionStorage;
using LopezAutoSales.Shared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace LopezAutoSales.Client.Services
{
    public class CarManager
    {
        private readonly ISyncSessionStorageService _sessionStorage;

        public CarManager(ISyncSessionStorageService sessionStorage)
        {
            _sessionStorage = sessionStorage;
        }

        public async Task<Car> GetCarAsync(HttpClient http, int id)
        {
            Car car = _sessionStorage.GetItem<Car>(id.ToString());
            if (car is null)
            {
                car = await http.GetFromJsonAsync<Car>($"/api/inventory/{id}");
                SetCar(car);
            }
            return car;
        }

        public void SetCar(Car car)
        {
            if (car is null) return;
            _sessionStorage.SetItem(car.Id.ToString(), car);
        }

        public void DeleteCar(Car car)
        {
            if (car is null) return;
            _sessionStorage.RemoveItem(car.Id.ToString());
        }
    }
}