using System;
using DasMulli.Win32.ServiceUtils;
using FoxyLink.RabbitMQ;

namespace FoxyLink
{
    public class Service : IWin32Service, IDisposable
    {
        private bool _disposed = false;
        private readonly string[] _commandLineArguments;
        public string ServiceName { get; }

        public Service(string[] commandLineArguments)
        {
            _commandLineArguments = commandLineArguments;
            ServiceName = Configuration.Current["HostData:ServiceName"];
        }

        public void Start(string[] startupArguments, ServiceStoppedCallback serviceStoppedCallback)
        {
            GlobalConfiguration.Configuration.UseRabbitMQHost();
        }

        public void Stop()
        {
            QueueHost.Current.Close();
        }

        ~Service()
        {
            Dispose(false);
        }

        public void Dispose()
        {
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
