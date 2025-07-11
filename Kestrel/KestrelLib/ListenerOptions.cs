using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace KestrelLib
{
    public class ListenerOptions
    {
        public IPAddress IPAddress { get; set; }
        public int Port { get; set; }
        public string? CertPath { get; set; }
        public string? CertPassword { get; set; }
        public bool UseHttps { get; set; }
        public HttpProtocols Protocols { get; set; } 
        public bool UseConnectionLogging { get; set; } 

        public ListenerOptions()
        {
            IPAddress = IPAddress.Any;
            UseHttps = false;
            Protocols = HttpProtocols.Http1;
            UseConnectionLogging = false;
        }
    }
}