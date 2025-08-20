using Microsoft.AspNetCore.Server.Kestrel.Core;
namespace Kestrun.Hosting.Options;

/// <summary>
/// Simple options class for configuring Kestrel server settings.
/// </summary>
/// <remarks>
/// This class provides a strongly-typed alternative to using a hashtable for Kestrel server options.
/// </remarks>
public class KestrunOptions
{
    /// <summary>
    /// Gets or sets the Kestrel server options.
    /// </summary>
    public KestrelServerOptions ServerOptions { get; set; }

    /// <summary>Provides access to request limit options. Use a hashtable or a KestrelServerLimits instance.</summary>
    public KestrelServerLimits ServerLimits => ServerOptions.Limits;

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
    /// Gets or sets the maximum number of runspaces to use for the scheduler service.
    /// Defaults to 8.
    /// </summary>
    public int MaxSchedulerRunspaces { get; set; }

    /// <summary>
    /// List of configured listeners for the Kestrel server.
    /// Each listener can be configured with its own IP address, port, protocols, and other options.
    /// </summary>
    public List<ListenerOptions> Listeners { get; }

    // Add more properties as needed for your scenario.

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunOptions"/> class with default values.
    /// </summary>
    public KestrunOptions()
    {
        // Set default values if needed
        MinRunspaces = 1; // Default to 1 runspace
        Listeners = [];
        ServerOptions = new KestrelServerOptions();
        MaxSchedulerRunspaces = 8; // Default max scheduler runspaces
    }
}
