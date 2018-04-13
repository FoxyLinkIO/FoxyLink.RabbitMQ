using System;

namespace FoxyLink
{
    public abstract class QueueHost
    {
        private static readonly object LockObject = new object();
        private static QueueHost _current;

        public static QueueHost Current
        {
            get
            {
                lock (LockObject)
                {
                    if (_current == null)
                    {
                        throw new InvalidOperationException("QueueHost.Current property value has not been initialized. You must set it before using DataBank Client or Server API.");
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

        public abstract void Close();
    }
}
