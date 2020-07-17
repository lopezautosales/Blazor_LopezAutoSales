using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LopezAutoSales.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            builder.Services.AddHttpClient("LopezAutoSales.ServerAPI", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
                .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();
            builder.Services.AddHttpClient("NoAuth", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

            // Supply HttpClient instances that include access tokens when making requests to the server project
            builder.Services.AddTransient(sp => new AuthHttp { Client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("LopezAutoSales.ServerAPI") });
            builder.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("NoAuth"));

            builder.Services.AddApiAuthorization()
                .AddAccountClaimsPrincipalFactory<CustomUserFactory>();

            await builder.Build().RunAsync();
        }
    }
}
