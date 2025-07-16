using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using FoxyLink.RabbitMQ;

namespace FoxyLink
{
    public class Service : IHostedService, IDisposable
    {
        private bool _disposed = false;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public string ServiceName { get; }

        public Service(ILogger<Service> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;

            ServiceName = config["HostData:ServiceName"];
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting service: " + ServiceName);

            GlobalConfiguration.Configuration.ConfigureAppEndpoints(_config);
            GlobalConfiguration.Configuration.UseRabbitMQHost(_config);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping service.");
            QueueHost.Current.Close();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing....");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposed = true;
            }
        }
    }
}
