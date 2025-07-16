using System;
using System.Collections.Generic;

namespace FoxyLink.RabbitMQ;

public class RabbitMQHostOptions
{
    public string AmqpUri { get; set; }
    public List<Queue> Queues { get; set; } = new List<Queue>();
    public List<string> RetryInMilliseconds { get; set; } = new List<string>();

    public class Queue
    {
        public string Name { get; set; }
        public int NodesCount { get; set; }
        public UInt16 PrefetchCount { get; set; }
        public bool QuorumQueue { get; set; }
    }
}
