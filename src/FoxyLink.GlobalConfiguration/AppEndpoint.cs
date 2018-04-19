using System;
using System.Net.Http.Headers;
using System.Text;

namespace FoxyLink
{
    public class AppEndpoint
    {
        private readonly AppEndpointOptions _options;

        private AuthenticationHeaderValue _authenticationHeader;
        private string _stringURI;

        public AuthenticationHeaderValue AuthenticationHeader => _authenticationHeader;
        public string StringURI => _stringURI;

        public AppEndpoint()
           : this(new AppEndpointOptions())
        {
        }

        public AppEndpoint(AppEndpointOptions options)
        {
            _options = options;

            var credentials = $"{_options.Login}:{_options.Password}";
            var bytes = Encoding.ASCII.GetBytes(credentials);
            _authenticationHeader = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(bytes));

            _stringURI = $"{_options.Schema}://{_options.ServerName}/{_options.PathOnServer}";
        }
    }
}
