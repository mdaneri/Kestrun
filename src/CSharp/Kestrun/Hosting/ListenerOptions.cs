using System.Net;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Kestrun;

/// <summary>
/// Configuration for an individual Kestrel listener.
/// </summary>
public class ListenerOptions
{
    /// <summary>The IP address to bind to.</summary>
    public IPAddress IPAddress { get; set; }

    /// <summary>The port to listen on.</summary>
    public int Port { get; set; }

    /// <summary>Whether HTTPS should be used.</summary>
    public bool UseHttps { get; set; }

    /// <summary>HTTP protocols supported by the listener.</summary>
    public HttpProtocols Protocols { get; set; }

    /// <summary>Enable verbose connection logging.</summary>
    public bool UseConnectionLogging { get; set; }

    /// <summary>Optional TLS certificate.</summary>
    public X509Certificate2? X509Certificate { get; internal set; }

    public ListenerOptions()
    {
        IPAddress = IPAddress.Any;
        UseHttps = false;
        Protocols = HttpProtocols.Http1;
        UseConnectionLogging = false;
    }
}
