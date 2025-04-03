using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NmeaBroadcastService
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseWindowsService()
        .ConfigureAppConfiguration(
            (hostingContext, config) =>
            {
                var baseDir = AppContext.BaseDirectory;
                config.SetBasePath(baseDir);
                config.AddJsonFile(
                    Path.Combine(baseDir, "appsettings.json"),
                    optional: false,
                    reloadOnChange: true
                );
                // Log the configuration file path for debugging
                Console.WriteLine($"Loading configuration from: {Path.Combine(baseDir, "appsettings.json")}");
            }
        )
        .ConfigureServices(
            (hostContext, services) =>
            {
                services.AddHostedService<NmeaBroadcastService>();
                services.AddSingleton<Services.NmeaDecoder>();
            }
        );
    }
