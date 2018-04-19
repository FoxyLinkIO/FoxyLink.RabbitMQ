using System;
using System.Collections.Generic;
using System.Text;

namespace FoxyLink
{
    public class AppEndpointOptions
    {
        public string Schema { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public string ServerName { get; set; }
        public string PathOnServer { get; set; }
    }
}
