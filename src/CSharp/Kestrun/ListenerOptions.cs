using System.Net;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Kestrun
{
    public class ListenerOptions
    {
        public IPAddress IPAddress { get; set; }
        public int Port { get; set; }
        public bool UseHttps { get; set; }
        public HttpProtocols Protocols { get; set; } 
        public bool UseConnectionLogging { get; set; }
        public X509Certificate2? X509Certificate { get; internal set; }

        public ListenerOptions()
        {
            IPAddress = IPAddress.Any;
            UseHttps = false;
            Protocols = HttpProtocols.Http1;
            UseConnectionLogging = false;
        }
    }
}