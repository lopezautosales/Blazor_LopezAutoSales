using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace LopezAutoSales.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // The Linux container defaults to InvariantCulture, which renders "¤" instead
            // of "$" for server-side ToString("C") on the public pages. Pin en-US.
            System.Globalization.CultureInfo enUs = new System.Globalization.CultureInfo("en-US");
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = enUs;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = enUs;
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            // The app stores local-time DateTimes (e.g. DateTime.Now) as it did on SQL
            // Server; this keeps Npgsql from rejecting non-UTC values and maps DateTime to
            // "timestamp without time zone". Set here (not in Main) so EF's design-time
            // tooling, which calls CreateHostBuilder but not Main, builds the same model.
            System.AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            return Host.CreateDefaultBuilder(args)
            .UseSerilog((hostingContext, services, loggerConfiguration) =>
            loggerConfiguration.MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
            ).ConfigureWebHostDefaults(webBuilder =>
            {
                // Railway (and most container hosts) inject the port to listen on.
                string port = System.Environment.GetEnvironmentVariable("PORT") ?? "8080";
                webBuilder.UseUrls($"http://0.0.0.0:{port}");
                webBuilder.UseStartup<Startup>();
            });
        }
    }
}
