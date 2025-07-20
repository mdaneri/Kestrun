// KestrelSimpleOptions.cs
//
// Simple POCO class with all configurable KestrelServerOptions properties as public settable properties.
// Use this as a strongly-typed alternative to the _kestrelOptions hashtable.
//
// Example usage:
//   var opts = new KestrelSimpleOptions { AllowSynchronousIO = true, AddServerHeader = false };
//   server.ApplyConfiguration(opts);

using System;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Collections;
namespace Kestrun;

/// <summary>
/// Simple options class for configuring Kestrel server settings.
/// </summary>
/// <remarks>
/// This class provides a strongly-typed alternative to using a hashtable for Kestrel server options.
/// </remarks>
public class KestrunOptions
{
    /// <summary>Gets or sets whether the Server header should be included in each response. Defaults to true.</summary>
    public bool? AddServerHeader { get; set; }

    /// <summary>Gets or sets a value that controls whether dynamic compression of response headers is allowed. Defaults to true.</summary>
    public bool? AllowResponseHeaderCompression { get; set; }

    /// <summary>Gets or sets a value that controls whether synchronous IO is allowed for the HttpContext.Request and HttpContext.Response. Defaults to false.</summary>
    public bool? AllowSynchronousIO { get; set; }

    /// <summary>Gets or sets a value that controls how the :scheme field for HTTP/2 and HTTP/3 requests is validated. Defaults to false.</summary>
    public bool? AllowAlternateSchemes { get; set; }

    /// <summary>Gets or sets a value that controls whether the string values materialized will be reused across requests; if they match, or if the strings will always be reallocated. Defaults to false.</summary>
    public bool? DisableStringReuse { get; set; }

    /// <summary>Gets or sets whether the Host header check is bypassed and overwritten from the request target. Defaults to false.</summary>
    public bool? AllowHostHeaderOverride { get; set; }

    /// <summary>Gets or sets a callback that returns the Encoding to decode the value for the specified request header name, or null to use the default UTF8Encoding.</summary>
    public Func<string, Encoding?>? RequestHeaderEncodingSelector { get; set; }

    /// <summary>Gets or sets a callback that returns the Encoding to encode the value for the specified response header or trailer name, or null to use the default ASCIIEncoding.</summary>
    public Func<string, Encoding?>? ResponseHeaderEncodingSelector { get; set; }

    /// <summary>Provides access to request limit options. Use a hashtable or a KestrelServerLimits instance.</summary>
    public KestrelServerLimits Limits { get; } = new KestrelServerLimits();

    /// <summary>Application name (optional, for diagnostics).</summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of runspaces to use for script execution.
    /// </summary>
    public int? MaxRunspaces { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of runspaces to use for script execution.   
    /// Defaults to 1.
    /// </summary>
    public int MinRunspaces { get; set; }

    // Add more properties as needed for your scenario.
}
