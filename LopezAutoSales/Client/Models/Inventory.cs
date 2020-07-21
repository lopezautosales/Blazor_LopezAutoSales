using LopezAutoSales.Shared.Models;
using System.Collections.Generic;
using System.Linq;

namespace LopezAutoSales.Client.Models
{
    public class Inventory
    {
        public IEnumerable<Car> Cars { get; private set; }
        private SortMethod Method { get; set; }
        private bool Repeated { get; set; }

        public enum SortMethod
        {
            Year = 0,
            Make = 1,
            Model = 2,
            Mileage = 3,
            BoughtPrice = 4,
            ListPrice = 5,
            Salvage = 6
        }

        public Inventory(IEnumerable<Car> cars)
        {
            Cars = cars;
        }

        public void Sort(SortMethod method)
        {
            if (Method == method)
                Repeated = !Repeated;
            else
                Repeated = false;
            Method = method;

            switch (method)
            {
                case SortMethod.Year:
                    if (Repeated)
                        Cars = Cars.OrderBy(x => x.Year);
                    else
                        Cars = Cars.OrderByDescending(x => x.Year);
                    break;
                case SortMethod.Make:
                    if (Repeated)
                        Cars = Cars.OrderBy(x => x.Make);
                    else
                        Cars = Cars.OrderByDescending(x => x.Make);
                    break;
                case SortMethod.Model:
                    if (Repeated)
                        Cars = Cars.OrderBy(x => x.Model);
                    else
                        Cars = Cars.OrderByDescending(x => x.Model);
                    break;
                case SortMethod.Mileage:
                    if (Repeated)
                        Cars = Cars.OrderBy(x => x.Mileage);
                    else
                        Cars = Cars.OrderByDescending(x => x.Mileage);
                    break;
                case SortMethod.BoughtPrice:
                    if (Repeated)
                        Cars = Cars.OrderBy(x => x.BoughtPrice);
                    else
                        Cars = Cars.OrderByDescending(x => x.BoughtPrice);
                    break;
                case SortMethod.ListPrice:
                    if (Repeated)
                        Cars = Cars.OrderBy(x => x.ListPrice);
                    else
                        Cars = Cars.OrderByDescending(x => x.ListPrice);
                    break;
                case SortMethod.Salvage:
                    if (Repeated)
                        Cars = Cars.OrderBy(x => x.IsSalvage);
                    else
                        Cars = Cars.OrderByDescending(x => x.IsSalvage);
                    break;
            }
        }
    }
}
