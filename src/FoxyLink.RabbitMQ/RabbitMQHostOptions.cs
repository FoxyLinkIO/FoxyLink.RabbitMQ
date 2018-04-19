using System;
using System.Collections.Generic;
using System.Text;

namespace FoxyLink.RabbitMQ
{
    public class RabbitMQHostOptions
    {
        public string HostName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string InvalidMessageQueue { get; set; }
    }
}
