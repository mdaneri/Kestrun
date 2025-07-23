using System;
using System.Collections.Concurrent;
using Serilog;

namespace Kestrun.Logging;

public static class LoggerManager
{
    private static readonly ConcurrentDictionary<string, Serilog.ILogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, LoggerConfiguration> _configs = new(StringComparer.OrdinalIgnoreCase);

    // Add or replace a logger (optionally as the default Serilog logger)
    public static Serilog.ILogger Add(string name, Action<LoggerConfiguration>? config = null, bool setAsDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        var cfg = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Debug();

        config?.Invoke(cfg);

        var logger = cfg.CreateLogger();
        _configs[name] = cfg;

        if (_loggers.TryGetValue(name, out var oldLogger) && oldLogger is IDisposable d)
            d.Dispose();

        _loggers[name] = logger;
        if (setAsDefault)
            Log.Logger = logger;
        return logger;
    }

    public static Serilog.ILogger Register(string name, Serilog.ILogger logger, bool setAsDefault = false)
    {

        if (_loggers.TryGetValue(name, out var oldLogger) && oldLogger is IDisposable d)
            d.Dispose();

        _loggers[name] = logger;
        if (setAsDefault)
            Log.Logger = logger;

        return logger;
    }
    public static Serilog.LoggerConfiguration New(string name)
    {
        var cfg = new LoggerConfiguration().
        Enrich.WithProperty("LoggerName", name);
        _configs[name] = cfg;
        return cfg;
    }
    // Remove a logger by name
    public static bool Remove(string name)
    {
        if (_loggers.TryRemove(name, out var logger))
        {
            if (logger is IDisposable d) d.Dispose();
            _configs.TryRemove(name, out _);
            return true;
        }
        return false;
    }

    public static string DefaultLoggerName
    {
        get => _loggers.FirstOrDefault(x => x.Value == Log.Logger).Key;
        set => Log.Logger = _loggers.TryGetValue(value, out var logger) ? logger :
            throw new ArgumentException($"Logger '{value}' not found.", nameof(value));
    }

    public static Serilog.ILogger DefaultLogger
    {
        get => Log.Logger;
        set => Log.Logger = value ?? new LoggerConfiguration().CreateLogger();
    }

    // Get a logger by name (returns null if not found)
    public static Serilog.ILogger? Get(string name) => _loggers.TryGetValue(name, out var logger) ? logger : null;

    // List all logger names
    public static string[] List() => [.. _loggers.Keys];

    // Remove and dispose all loggers
    public static void Clear()
    {
        foreach (var (name, logger) in _loggers)
            if (logger is IDisposable d) d.Dispose();
        _loggers.Clear();
        _configs.Clear();
    }

}
