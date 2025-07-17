namespace KestrumLib
{
    /// <summary>
    /// Simple wrapper around Serilog that matches the cmdlet style logging API.
    /// </summary>
    public class PSLog
    {
        /// <summary>Logs a debug message.</summary>
        public void Debug(string message, params object[] args)      => Serilog.Log.Debug(message, args);
        /// <summary>Logs a verbose message.</summary>
        public void Verbose(string message, params object[] args)    => Serilog.Log.Verbose(message, args);
        /// <summary>Logs an informational message.</summary>
        public void Information(string message, params object[] args)=> Serilog.Log.Information(message, args);
        /// <summary>Logs a warning message.</summary>
        public void Warning(string message, params object[] args)    => Serilog.Log.Warning(message, args);
        /// <summary>Logs an error message.</summary>
        public void Error(string message, params object[] args)      => Serilog.Log.Error(message, args);
        /// <summary>Logs a fatal message.</summary>
        public void Fatal(string message, params object[] args)      => Serilog.Log.Fatal(message, args);
    }
}