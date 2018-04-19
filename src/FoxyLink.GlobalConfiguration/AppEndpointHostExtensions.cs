using System;
using System.Collections.Generic;
using System.Text;

namespace FoxyLink
{
    public static class AppEndpointHostExtensions
    {
        public static void ConfigureAppEndpoints(
            [NotNull] this IGlobalConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var config = Configuration.Current;
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
}
