using Microsoft.Extensions.Configuration;
using System;

namespace FoxyLink
{
    public static class GlobalConfigurationExtensions
    {
        public static IGlobalConfiguration<TQueueHost> UseQueueHost<TQueueHost>(
            [NotNull] this IGlobalConfiguration configuration,
            [NotNull] TQueueHost host)
            where TQueueHost : QueueHost
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (host == null) throw new ArgumentNullException(nameof(host));

            return configuration.Use(host, x => QueueHost.Current = x);
        }

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
