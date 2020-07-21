using System.Net.Http;

namespace LopezAutoSales.Client
{
    public class AuthHttp
    {
        public HttpClient Client { get; private set; }
        public AuthHttp(HttpClient http)
        {
            Client = http;
        }
    }
}
