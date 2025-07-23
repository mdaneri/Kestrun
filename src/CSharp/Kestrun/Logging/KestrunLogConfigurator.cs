// KestrunLogConfigurator.cs
// -----------------------------------------------------------------------------
// Central registry and factory for all loggers created through Kestrun.
//
//  * Keeps a name->LoggerConfiguration table so you can tweak/reload settings.
//  * Keeps a name->ILogger table so handlers can fetch a ready-to-use logger.
//  * Provides a Reset() helper for hot-reload, tests, or graceful shutdown.
//
// Copyright © 2025  Kestrun
// -----------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Serilog;
using Serilog.Events;

namespace Kestrun.Logging;

/// <summary>
/// Static façade that orchestrates <see cref="Serilog"/> loggers inside Kestrun.
///
/// Call <see cref="Configure(string)"/> to start a fluent build chain and
/// <see cref="Get(string)"/> whenever you need the finished logger in your
/// request handlers or background jobs.
/// </summary>
public static class KestrunLogConfigurator
{
    // ------------------------------------------------------------------------
    // Internal state
    // ------------------------------------------------------------------------

    /// <summary>
    /// Stores the *configuration objects* so they can be re-examined,
    /// cloned, or re-written (for hot-reload scenarios).
    /// </summary>
    private static readonly ConcurrentDictionary<string, LoggerConfiguration> _cfg =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Stores the *live loggers* keyed by the same name.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Serilog.ILogger> _log =
        new(StringComparer.OrdinalIgnoreCase);


    /// <summary>
    /// Creates (or retrieves) a <see cref="KestrunLoggerBuilder"/> for a
    /// specific logical name.
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> _configLocks
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lock object used to ensure that Reset() is thread-safe.
    /// </summary>
    private static readonly object _resetLock = new();

    public static Serilog.Formatting.Display.MessageTemplateTextFormatter TextFormatter { get; } = new("{Message:lj}");

    // ------------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------------

    /// <summary>
    /// Creates (or retrieves) a <see cref="KestrunLoggerBuilder"/> for a
    /// specific logical name.
    /// </summary>
    /// <remarks>
    /// If the name has never been seen, a new <see cref="LoggerConfiguration"/>
    /// initialised with <c>.Enrich.FromLogContext()</c> is created; otherwise
    /// the existing configuration is reused so subsequent calls extend it.
    /// </remarks>
    /// <param name="name">
    /// Unique identifier of the logger (case-insensitive).  
    /// Recommended pattern: use a subsystem name like <c>api</c>, <c>auth</c>,
    /// <c>background-jobs</c>, etc.
    /// </param>
    /// <returns>A builder that lets you add sinks, enrichers, minimum level…</returns>
    public static KestrunLoggerBuilder Configure(string name)
    {
        // get the per‐logger lock
        var sync = _configLocks.GetOrAdd(name, _ => new object());

        lock (sync)
        {
            var cfg = _cfg.GetOrAdd(name, _ => new LoggerConfiguration()
                                               .Enrich.FromLogContext());
            return new KestrunLoggerBuilder(name, cfg, sync);
        }
    }

    /// <summary>
    /// Looks up a live logger by name.
    /// </summary>
    /// <param name="name">Name supplied earlier to <see cref="Configure"/>.</param>
    /// <returns>The logger if found; otherwise <see langword="null"/>.</returns>
    public static Serilog.ILogger? Get(string name) =>
        _log.TryGetValue(name, out var l) ? l : null;


    /// <summary>
    /// Checks if a logger with the specified name exists.
    /// </summary>
    /// <param name="name">The name of the logger to check.</param>
    /// <returns><see langword="true"/> if the logger exists; otherwise <see langword="false"/>.</returns>
    public static bool Exists(string name) =>
        _log.ContainsKey(name);

    /// <summary>
    /// Enumerates every logger name currently registered.
    /// </summary>
    public static IEnumerable<string> Names() => _log.Keys;

    /// <summary>
    /// Flushes, disposes, and forgets **all** configured loggers, then
    /// reinstates a minimal console logger so framework code keeps working.
    ///
    /// Use this when:
    /// * hot-reloading JSON/YAML logger settings;
    /// * running unit/integration tests that must stay isolated;
    /// * shutting down gracefully (omit step 3 if the process is exiting
    ///   immediately after flush).
    /// </summary>
    public static void Reset()
    {
        lock (_resetLock)
        {
            // 1️⃣  Ensure all buffered events hit their sinks.
            Log.CloseAndFlush();

            // 2️⃣  Dispose every custom logger (only those that implement IDisposable).
            foreach (var logger in _log.Values.OfType<IDisposable>())
            {
                try { logger.Dispose(); }
                catch { /* Swallow: we are tearing down. */ }
            }

            _log.Clear();
            _cfg.Clear();
            _configLocks.Clear();

            // 3️⃣  Restore a benign “factory default” logger so any
            //      Serilog.Log.Information(…) still works after Reset().
            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Information()
                            .WriteTo.Console(
                                outputTemplate:
                                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}")
                            .CreateLogger();
        }
    }

    // ------------------------------------------------------------------------
    // Internal helpers (used by KestrunLoggerBuilder)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Registers a freshly-built logger and (optionally) swaps it in as the
    /// global <see cref="Log.Logger"/>.
    /// </summary>
    /// <param name="name">Logical name originally supplied by the caller.</param>
    /// <param name="cfg">Underlying Serilog configuration (kept for reload).</param>
    /// <param name="log">Live logger instance.</param>
    /// <param name="default">
    /// <see langword="true"/> to replace the shared static
    /// <see cref="Log.Logger"/>; <see langword="false"/> to leave it unchanged.
    /// </param>
    internal static void Register(string name,
                                  LoggerConfiguration cfg,
                                  Serilog.ILogger log,
                                  bool @default)
    {
        // same lock that Configure used
        var sync = _configLocks.GetOrAdd(name, _ => new object());
        lock (sync)
        {
            _cfg[name] = cfg;
            _log[name] = log;

            if (@default)
                Log.Logger = log;   // promote to global default
        }
    }

    /// <summary>
    /// Atomically update an existing logger.
    /// </summary>
    /// <param name="name">Logger to update (case-insensitive).</param>
    /// <param name="mutateCfg">Action that mutates the stored LoggerConfiguration.</param>
    /// <param name="setAsDefault">
    /// If <c>true</c>, the replacement logger also becomes <c>Serilog.Log.Logger</c>.
    /// </param>
    public static void Reconfigure(
        string name,
        Action<LoggerConfiguration> mutateCfg,
        bool setAsDefault = false)
    {
        // ①  Same per-logger lock used by Configure / Register
        var sync = _configLocks.GetOrAdd(name, _ => new object());

        lock (sync)
        {
            // ②  Fetch the existing configuration (or fail fast)
            if (!_cfg.TryGetValue(name, out var existingCfg))
                throw new InvalidOperationException($"Logger '{name}' not found.");

            // ③  Start from a *fresh* configuration — CreateLogger() is one-shot
            var newCfg = new LoggerConfiguration();
            mutateCfg(newCfg);                      // caller adds sinks / enrichers / level

            // ④  Dispose the superseded logger so sinks release resources
            if (_log.TryGetValue(name, out var oldLogger) && oldLogger is IDisposable disp)
                disp.Dispose();

            // ⑤  Build and publish the replacement
            var newLogger = newCfg.CreateLogger();
            _log[name] = newLogger;
            _cfg[name] = newCfg;

            // ⑥  Promote to global default if requested
            if (setAsDefault)
                Log.Logger = newLogger;
        }
    }
}
