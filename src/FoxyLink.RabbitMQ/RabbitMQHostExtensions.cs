using System;
using System.Collections.Generic;
using System.Text;

namespace FoxyLink.RabbitMQ
{
    public static class RabbitMQHostExtensions
    {
        public static IGlobalConfiguration<RabbitMQHost> UseRabbitMQHost(
            [NotNull] this IGlobalConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var queueHost = new RabbitMQHost();
            return configuration.UseQueueHost(queueHost);
        }

        public static IGlobalConfiguration<RabbitMQHost> UseRabbitMQHost(
            [NotNull] this IGlobalConfiguration configuration,
            [NotNull] RabbitMQHostOptions options)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var queueHost = new RabbitMQHost(options);
            return configuration.UseQueueHost(queueHost);
        }
    }
}
