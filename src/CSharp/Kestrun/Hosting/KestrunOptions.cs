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

    /// Gets the Kestrel server options.
    /// </summary>
    public KestrelServerOptions ServerOptions { get; }

    /// <summary>Provides access to request limit options. Use a hashtable or a KestrelServerLimits instance.</summary>
    public KestrelServerLimits ServerLimits { get => ServerOptions.Limits; }

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


    /// <summary>
    /// List of configured listeners for the Kestrel server.
    /// Each listener can be configured with its own IP address, port, protocols, and other options.
    /// </summary>
    public List<ListenerOptions> Listeners { get; }

    // Add more properties as needed for your scenario.

    public KestrunOptions()
    {
        // Set default values if needed
        MinRunspaces = 1; // Default to 1 runspace
        Listeners = [];
        ServerOptions = new KestrelServerOptions();
    }
   
}
