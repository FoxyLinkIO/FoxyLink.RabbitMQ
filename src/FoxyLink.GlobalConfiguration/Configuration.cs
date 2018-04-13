using Microsoft.Extensions.Configuration;
using System;

namespace FoxyLink
{
    public abstract class Configuration
    {
        private static readonly object LockObject = new object();
        private static IConfigurationRoot _current;

        public static IConfigurationRoot Current
        {
            get
            {
                lock (LockObject)
                {
                    if (_current == null)
                    {
                        throw new InvalidOperationException("Configuration.Current property value has not been initialized. You must set it before using DataBank Client or Server API.");
                    }

                    return _current;
                }
            }
            set
            {
                lock (LockObject)
                {
                    _current = value;
                }
            }
        }
    }
}