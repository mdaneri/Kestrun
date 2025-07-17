using System.Net;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace KestrumLib
{
    /// <summary>
    /// Options for configuring an individual HTTP listener in Kestrun.
    /// </summary>
    public class ListenerOptions
    {
        /// <summary>IP address to bind to.</summary>
        public IPAddress IPAddress { get; set; }
        /// <summary>TCP port to listen on.</summary>
        public int Port { get; set; }
        /// <summary>Whether HTTPS should be used.</summary>
        public bool UseHttps { get; set; }
        /// <summary>HTTP protocols supported by this listener.</summary>
        public HttpProtocols Protocols { get; set; }
        /// <summary>Enables connection logging middleware.</summary>
        public bool UseConnectionLogging { get; set; }
        /// <summary>Certificate used for TLS termination when <see cref="UseHttps"/> is true.</summary>
        public X509Certificate2? X509Certificate { get; internal set; }

        /// <summary>
        /// Creates a new instance with sensible defaults.
        /// </summary>
        public ListenerOptions()
        {
            IPAddress = IPAddress.Any;
            UseHttps = false;
            Protocols = HttpProtocols.Http1;
            UseConnectionLogging = false;
        }
    }
}