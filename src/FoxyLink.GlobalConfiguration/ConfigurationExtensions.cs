using Microsoft.Extensions.Configuration;
using System;

namespace FoxyLink
{
    public static class ConfigurationExtensions
    {
        public static IGlobalConfiguration UseConfiguration(
            [NotNull] this IGlobalConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                    .Build();

            return configuration.UseConfiguration(config);
        }
    }
}
