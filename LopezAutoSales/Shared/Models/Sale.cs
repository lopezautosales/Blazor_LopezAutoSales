using System;
using System.Collections.Generic;
using System.Text;

namespace LopezAutoSales.Shared.Models
{
    public class Sale
    {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public string Phone { get; set; }
        public string Buyer { get; set; }
        public string CoBuyer { get; set; }
    }
}
