using Microsoft.Extensions.Configuration;
using System;

namespace FoxyLink
{
    public static class GlobalConfigurationExtensions
    {
        public static IGlobalConfiguration<TConfiguration> UseConfiguration<TConfiguration>(
            [NotNull] this IGlobalConfiguration configuration,
            [NotNull] TConfiguration config)
            where TConfiguration : IConfigurationRoot
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (config == null) throw new ArgumentNullException(nameof(config));

            return configuration.Use(config, x => Configuration.Current = x);
        }

        //public static IGlobalConfiguration<TWebHost> UseWebHost<TWebHost>(
        //    [NotNull] this IGlobalConfiguration configuration,
        //    [NotNull] TWebHost host)
        //    where TWebHost : WebHost
        //{
        //    if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        //    if (host == null) throw new ArgumentNullException(nameof(host));
        //
        //    return configuration.Use(host, x => WebHost.Current = x);
        //}

        public static IGlobalConfiguration<T> Use<T>(
            [NotNull] this IGlobalConfiguration configuration, T entry,
            [NotNull] Action<T> entryAction)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            entryAction(entry);

            return new ConfigurationEntry<T>(entry);
        }

        private class ConfigurationEntry<T> : IGlobalConfiguration<T>
        {
            public ConfigurationEntry(T entry)
            {
                Entry = entry;
            }

            public T Entry { get; }
        }
    }
}
