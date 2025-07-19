// File: KestrunLogTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Kestrun;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Kestrun.Tests.Logging;

/// <summary>
/// Simple in-memory sink so we can inspect what the logger produced.
/// </summary>
internal sealed class InMemorySink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];
    public IReadOnlyList<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Add(logEvent);
}

public class KestrunLogTests : IDisposable
{
    // ────────────────── helpers ──────────────────
    private static (Serilog.ILogger logger, InMemorySink sink) BuildTestLogger(string name)
    {
        var sink = new InMemorySink();

        var logger = KestrunLogConfigurator.Configure(name)
            .Minimum(LogEventLevel.Information)
            .WithProperty("Subsystem", name)               // prove enrichers work
            .Sink(w => w.Sink(sink))                       // attach our capture sink
            .Register(setAsDefault: false);

        return (logger, sink);
    }

    // ─────────────── actual tests ────────────────

    [Fact]
    public void Configure_And_Register_CreateNamedLogger()
    {
        var (logger, _) = BuildTestLogger("unit-test");

        Assert.NotNull(logger);
        Assert.True(KestrunLogConfigurator.Exists("unit-test"));
        Assert.Same(logger, KestrunLogConfigurator.Get("unit-test"));
    }

    [Fact]
    public void LoggedEvent_Includes_CustomProperty()
    {
        var (logger, sink) = BuildTestLogger("api");

        logger.Information("Hello {User}", "world");

        var evt = Assert.Single(sink.Events);
        Assert.True(evt.Properties.ContainsKey("Subsystem"));
        Assert.Equal("api", evt.Properties["Subsystem"].LiteralValue());
    }

    [Fact]
    public void Reconfigure_ReplacesLoggerAndAppliesNewLevel()
    {
        // original logger at Information
        var sink1 = new InMemorySink();
        KestrunLogConfigurator.Configure("dynamic")
            .Sink(w => w.Sink(sink1))
            .Register();

        // raise the minimum level to Error
        KestrunLogConfigurator.Reconfigure("dynamic",
            cfg => cfg.MinimumLevel.Is(LogEventLevel.Error));

        var sink2 = new InMemorySink();
        // plug the new sink so we can verify behaviour after reconfigure
        KestrunLogConfigurator.Reconfigure("dynamic", cfg =>
        {
            cfg.MinimumLevel.Is(LogEventLevel.Error);
            cfg.WriteTo.Sink(sink2);
        });

        var logger = KestrunLogConfigurator.Get("dynamic")!;
        logger.Information("This should be dropped");
        logger.Error("This should be captured");

        Assert.Empty(sink1.Events);         // the Information was already filtered
        var captured = Assert.Single(sink2.Events);
        Assert.Equal(LogEventLevel.Error, captured.Level);
    }

    [Fact]
    public void Reset_Clears_All_Loggers()
    {
        BuildTestLogger("to-reset");
        Assert.True(KestrunLogConfigurator.Exists("to-reset"));

        KestrunLogConfigurator.Reset();
        Assert.False(KestrunLogConfigurator.Exists("to-reset"));
        Assert.Null(KestrunLogConfigurator.Get("to-reset"));
    }

    // ────────────── cleanup ──────────────
    public void Dispose() => KestrunLogConfigurator.Reset();
}

// tiny helper to read scalar values cleanly
internal static class SerilogExtensions
{
    public static string LiteralValue(this LogEventPropertyValue v) =>
        v is ScalarValue s && s.Value != null ? s.Value.ToString()! : "<null>";
}
