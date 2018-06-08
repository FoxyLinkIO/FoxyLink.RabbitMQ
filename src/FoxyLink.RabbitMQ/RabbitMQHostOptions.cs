using System;
using System.Collections.Generic;
using System.Text;

namespace FoxyLink.RabbitMQ
{
    public class RabbitMQHostOptions
    {
        public string AmqpUri { get; set; }
        public string MessageQueue { get; set; }
        public string InvalidMessageQueue { get; set; }
        public Int32 NodesCount { get; set; }
        public UInt16 PrefetchCount { get; set; }
    }
}
