using System;
using System.Collections.Generic;
using System.Text;

namespace FoxyLink
{
    public abstract class AppEndpointHost
    {
        private static readonly object LockObject = new object();
        private static Dictionary<string, AppEndpoint> _current = new Dictionary<string, AppEndpoint>();

        public static AppEndpoint Get(string key)
        {
            lock (LockObject)
            {
                if (_current == null)
                {
                    throw new InvalidOperationException("AppEndpointHost.Current property value has not been initialized. You must set it before using AppEndpointHost.");
                }

                return _current.GetValueOrDefault(key.ToUpper());
            }
        }

        public static void Add(string key, AppEndpoint appEndpoint)
        {
            lock (LockObject)
            {
                if (_current == null)
                {
                    throw new InvalidOperationException("AppEndpointHost.Current property value has not been initialized. You must set it before using AppEndpointHost.");
                }

                _current.Add(key.ToUpper(), appEndpoint);
            }
        }
    }
}
