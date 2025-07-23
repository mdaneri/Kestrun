// KestrunLoggerBuilder.cs
// -----------------------------------------------------------------------------
// Part of the Kestrun web-server framework.
// A fluent builder that lets callers assemble an independent Serilog logger
// (sinks, enrichers, minimum level, etc.) and optionally promote it to the
// framework-wide default.
//
// Copyright © 2025  Kestrun
// -----------------------------------------------------------------------------

using System;
using Kestrun.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Kestrun;

/// <summary>
/// Fluent helper for composing named <see cref="ILogEventSink">Serilog loggers</see>
/// at runtime.
///
/// <para>
///   The builder wraps a single <see cref="LoggerConfiguration"/> instance so that
///   you can chain Serilog’s native extension methods yet still keep Kestrun’s
///   own registry in sync.  Typical usage:
/// </para>
///
/// <code>
/// KestrunLogConfigurator.Configure("api")
///     .Minimum(LogEventLevel.Debug)
///     .Enrich(e => e.WithProperty("Subsystem", "API"))
///     .Sink(w => w.File("logs/api-.log", rollingInterval: RollingInterval.Day))
///     .Apply();
/// </code>
/// </summary>
public sealed class KestrunLoggerBuilder
{
    // ------------------------------------------------------------------------
    // Fields
    // ------------------------------------------------------------------------

    private readonly string _name;                 // logical name inside Kestrun
    private readonly LoggerConfiguration _cfg;     // underlying Serilog config
    private readonly object _sync;                  // per-logger lock

    // ------------------------------------------------------------------------
    // ctor
    // ------------------------------------------------------------------------

    /// <summary>
    /// Called only by <see cref="KestrunLogConfigurator"/>.  Users get instances
    /// via <c>Configure(string)</c>; they never construct the builder directly.
    /// </summary>
    /// <param name="name">Name under which the logger will be registered.</param>
    /// <param name="cfg">A <see cref="LoggerConfiguration"/> already initialised
    ///                   with sensible defaults (enrichers, filters, etc.).</param>
    internal KestrunLoggerBuilder(string name, LoggerConfiguration cfg, object sync)
    {
        _name = name;
        _cfg = cfg;
        _sync = sync;
    }

    // ------------------------------------------------------------------------
    // Fluent modifiers
    // ------------------------------------------------------------------------

    /// <summary>
    /// Sets the minimum level for the logger being built.
    /// </summary>
    /// <remarks>
    /// Equivalent to calling <c>MinimumLevel.Is(level)</c> on Serilog, but keeps
    /// the builder chain intact.
    /// </remarks>
    /// <param name="level">The threshold below which events will be dropped.</param>
    /// <returns>The current <see cref="KestrunLoggerBuilder"/> so calls can
    ///          continue to be chained.</returns>
    public KestrunLoggerBuilder Minimum(LogEventLevel level)
        => Tap(_cfg.MinimumLevel.Is(level));

    /// <summary>
    /// Applies custom enrichers to every log event produced by this logger.
    /// </summary>
    /// <param name="enrich">
    /// Delegate that receives Serilog’s <see cref="LoggerEnrichmentConfiguration"/>
    /// so callers can attach enrichers (<c>.WithProperty()</c>, <c>.WithMachineName()</c>, …).
    /// </param>
    /// <returns><c>this</c>, enabling further chaining.</returns>
    public KestrunLoggerBuilder Enrich(
        Func<LoggerEnrichmentConfiguration, LoggerConfiguration> enrich)
        => Tap(enrich(_cfg.Enrich));

    /// <summary>
    /// Adds a property to the logger that will be included in every log event. 
    /// /// </summary>
    /// <param name="name">Name of the property to add.</param>
    /// <param name="value">Value of the property to add.</param>
    /// <returns><c>this</c>, enabling further chaining.</returns>
    public KestrunLoggerBuilder WithProperty(string name, object value) =>
        Enrich(e => e.WithProperty(name, value));

    /// <summary>
    /// Adds an enricher whose type has a public parameter-less constructor.
    /// Mirrors Serilog's native <c>.Enrich.With&lt;TEnricher&gt;()</c>.
    /// </summary>
    public KestrunLoggerBuilder With<TEnricher>()
        where TEnricher : ILogEventEnricher, new()
        => Enrich(e => e.With<TEnricher>());

    /// <summary>
    /// Adds an enricher constructed with arbitrary arguments.
    /// Use this when the enricher requires configuration via its constructor.
    /// </summary>
    /// <param name="ctorArgs">Arguments forwarded to <c>TEnricher</c>'s constructor.</param>
    public KestrunLoggerBuilder With<TEnricher>(params object[] ctorArgs)
        where TEnricher : ILogEventEnricher          // no new() constraint here
    {
        var enricher = (ILogEventEnricher?)
                      Activator.CreateInstance(typeof(TEnricher), ctorArgs)
                      ?? throw new InvalidOperationException(
                             $"Failed to create enricher {typeof(TEnricher).Name}");

        return Enrich(e => e.With(enricher));
    }

    /// <summary>
    /// Adds one or more sinks to the logger.
    /// </summary>
    /// <param name="add">
    /// Delegate that is passed <see cref="LoggerSinkConfiguration"/>.  
    /// Use it to call any Serilog sink extension such as
    /// <c>.File()</c>, <c>.Console()</c>, <c>.Seq()</c>, etc.
    /// </param>
    /// <returns>The same builder instance.</returns>
    public KestrunLoggerBuilder Sink(
        Func<LoggerSinkConfiguration, LoggerConfiguration> add)
        => Tap(add(_cfg.WriteTo));

    // ------------------------------------------------------------------------
    // Finalise
    // ------------------------------------------------------------------------

    /// <summary>
    /// Finishes the configuration and returns a live <see cref="ILogger"/>.
    ///
    /// The new logger is registered inside <see cref="KestrunLogConfigurator"/>
    /// under the original name supplied to <c>Configure()</c>.
    /// </summary>
    /// <param name="setAsDefault">
    /// When <see langword="true"/>, the new logger also replaces
    /// <c>Serilog.Log.Logger</c> so that all framework-level calls use it.
    /// </param>
    /// <returns>The constructed Serilog logger.</returns>
    public Serilog.ILogger Register(bool setAsDefault = false)
    {
        lock (_sync)
        {
            var logger = _cfg.CreateLogger();
            KestrunLogConfigurator.Register(_name, _cfg, logger, setAsDefault);            
            return logger;
        }
    }

    // ------------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------------

    /// <summary>
    /// Small utility to execute a configuration operation and continue
    /// the fluent chain without extra boilerplate.
    /// </summary>
    private KestrunLoggerBuilder Tap(LoggerConfiguration _)
        => this;
}
