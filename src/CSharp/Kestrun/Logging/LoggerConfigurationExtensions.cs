using Serilog;

namespace Kestrun.Logging;

/// <summary>
/// Convenience extensions for hooking Serilog loggers into <see cref="LoggerManager"/>.
/// </summary>
public static class LoggerConfigurationExtensions
{
    /// <summary>
    /// Create a logger from this configuration and register it by name.
    /// </summary>
    public static Serilog.ILogger Register(this LoggerConfiguration config, string name, bool setAsDefault = false)
    {
        var logger = config.CreateLogger();
        _ = LoggerManager.Register(name, logger, setAsDefault);
        return logger;
    }
}
