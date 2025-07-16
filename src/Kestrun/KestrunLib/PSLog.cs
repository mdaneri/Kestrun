namespace KestrumLib
{
    public class PSLog
    {
        public void Debug(string message, params object[] args)      => Serilog.Log.Debug(message, args);
        public void Verbose(string message, params object[] args)    => Serilog.Log.Verbose(message, args);
        public void Information(string message, params object[] args)=> Serilog.Log.Information(message, args);
        public void Warning(string message, params object[] args)    => Serilog.Log.Warning(message, args);
        public void Error(string message, params object[] args)      => Serilog.Log.Error(message, args);
        public void Fatal(string message, params object[] args)      => Serilog.Log.Fatal(message, args);
    }
}