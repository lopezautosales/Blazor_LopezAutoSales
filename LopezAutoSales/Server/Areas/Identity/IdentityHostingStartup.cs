using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(LopezAutoSales.Server.Areas.Identity.IdentityHostingStartup))]
namespace LopezAutoSales.Server.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
            });
        }
    }
}