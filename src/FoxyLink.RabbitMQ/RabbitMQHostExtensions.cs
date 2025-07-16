using Microsoft.Extensions.Configuration;
using System;

namespace FoxyLink.RabbitMQ;

public static class RabbitMQHostExtensions
{
    public static IGlobalConfiguration<RabbitMQHost> UseRabbitMQHost(
        [NotNull] this IGlobalConfiguration configuration, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        var options = new RabbitMQHostOptions()
        {
            AmqpUri = config["AccessData:RabbitMQ:AmqpUri"]
        };

        var sections = config.GetSection("AccessData:RabbitMQ:Queues");
        foreach (var section in sections.GetChildren())
        {
            if (!Int32.TryParse(config[$"{section.Path}:NodesCount"], out var nodes))
            {
                nodes = 1;
            }

            if (!UInt16.TryParse(config[$"{section.Path}:PrefetchCount"], out var prefetch))
            {
                prefetch = 1;
            }

            if (!bool.TryParse(config[$"{section.Path}:QuorumQueue"], out var queueType))
            {
                queueType = false;
            }

            options.Queues.Add(new RabbitMQHostOptions.Queue()
            {
                Name = config[$"{section.Path}:Name"],
                NodesCount = nodes,
                PrefetchCount = prefetch,
                QuorumQueue = queueType
            });
        }

        sections = config.GetSection("AccessData:RabbitMQ:RetryInMilliseconds");
        foreach (var section in sections.GetChildren())
        {
            options.RetryInMilliseconds.Add(section.Value);
        }

        return configuration.UseRabbitMQHost(options);
    }

    public static IGlobalConfiguration<RabbitMQHost> UseRabbitMQHost(
        [NotNull] this IGlobalConfiguration configuration,
        [NotNull] RabbitMQHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        var queueHost = new RabbitMQHost(options);
        queueHost.CreateConnectionAsync().Wait();
        return configuration.UseQueueHost(queueHost);
    }
}
