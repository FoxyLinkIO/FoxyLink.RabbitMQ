using Microsoft.Extensions.Configuration;
using System;

namespace FoxyLink;

public static class AppEndpointHostExtensions
{
    public static void ConfigureAppEndpoints(
        [NotNull] this IGlobalConfiguration configuration, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        var sections = config.GetSection("AccessData:AppEndpoints");
        foreach (var section in sections.GetChildren())
        {
            var options = new AppEndpointOptions()
            {
                Login = config[$"{section.Path}:Login"],
                Password = config[$"{section.Path}:Password"],
                Schema = config[$"{section.Path}:Schema"],
                ServerName = config[$"{section.Path}:ServerName"],
                PathOnServer = config[$"{section.Path}:PathOnServer"]
            };

            AppEndpointHost.Add(config[$"{section.Path}:Name"],
                new AppEndpoint(options));
        }
    }
}
