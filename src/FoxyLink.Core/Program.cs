using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FoxyLink
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var isService = !(Debugger.IsAttached || args.Contains("--console") || args.Contains("--docker"));

            var builder = new HostBuilder()
                .ConfigureHostConfiguration((config) =>
                {
                    config.AddEnvironmentVariables();
                })
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }

                    config.AddJsonFile(@"appsettings.json", optional: false);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();
                    services.AddSingleton<IHostedService, Service>();
                })
                .ConfigureLogging((hostingContext, logging) => {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                });

            if (isService)
            {
                await builder.RunAsServiceAsync();
            }
            else
            {
                await builder.RunConsoleAsync();
            }
        }
    }
}
