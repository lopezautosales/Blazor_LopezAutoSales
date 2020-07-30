using Blazored.SessionStorage;
using LopezAutoSales.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
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
            builder.Services.AddTransient(x => new AuthHttp(x.GetRequiredService<IHttpClientFactory>().CreateClient("LopezAutoSales.ServerAPI")));
            builder.Services.AddTransient(x => x.GetRequiredService<IHttpClientFactory>().CreateClient("NoAuth"));

            builder.Services.AddApiAuthorization()
                .AddAccountClaimsPrincipalFactory<CustomUserFactory>();
            builder.Services.AddBlazoredSessionStorage();
            builder.Services.AddTransient(x => new CarManager(x.GetService<ISyncSessionStorageService>()));
            builder.Services.AddTransient(x => new VINDecoder(x.GetService<HttpClient>(), x.GetService<IJSRuntime>()));

            await builder.Build().RunAsync();
        }
    }
}
