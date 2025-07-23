using Serilog;

namespace Kestrun.Logging;

public static class LoggerConfigurationExtensions
{
    public static Serilog.ILogger Register(this LoggerConfiguration config, string name, bool setAsDefault = false)
    {
        var logger = config.CreateLogger();
        // store in your LoggerManager, etc.
        LoggerManager.Register(name, logger, setAsDefault);
        return logger;
    }
}
