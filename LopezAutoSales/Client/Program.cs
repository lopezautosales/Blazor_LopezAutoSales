using Blazored.SessionStorage;
using LopezAutoSales.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
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
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddTransient<CookieHandler>();
            builder.Services.AddHttpClient("LopezAutoSales.ServerAPI", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
                .AddHttpMessageHandler<CookieHandler>();
            // No cookie handler: this client is for credential-free calls (e.g. the
            // external NHTSA VIN lookup); it must not attach the auth cookie.
            builder.Services.AddHttpClient("NoAuth", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

            // Supply HttpClient instances that include the auth cookie when making requests to the server project
            builder.Services.AddScoped(x => new AuthHttp(x.GetRequiredService<IHttpClientFactory>().CreateClient("LopezAutoSales.ServerAPI")));
            builder.Services.AddScoped(x => x.GetRequiredService<IHttpClientFactory>().CreateClient("NoAuth"));

            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<CookieAuthenticationStateProvider>();
            builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthenticationStateProvider>());

            builder.Services.AddBlazoredSessionStorage();
            builder.Services.AddTransient(x => new CarManager(x.GetService<ISyncSessionStorageService>()));
            builder.Services.AddTransient(x => new VINDecoder(x.GetService<HttpClient>(), x.GetService<IJSRuntime>()));
            await builder.Build().RunAsync();
        }
    }
}
