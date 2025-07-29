using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kestrun.Hosting;
using Serilog;
using Serilog.Events;

namespace Kestrun;

public static class KestrunHostManager
{
    private static readonly ConcurrentDictionary<string, KestrunHost> _instances = new(StringComparer.OrdinalIgnoreCase);
    private static string? _defaultName;

    public static IReadOnlyCollection<string> InstanceNames => (IReadOnlyCollection<string>)_instances.Keys;

    public static string? KestrunRoot { get; private set; }

    public static void SetKestrunRoot(string? kestrunRoot)
    {
        if (string.IsNullOrWhiteSpace(kestrunRoot))
            throw new ArgumentException("Kestrun root path cannot be null or empty.", nameof(kestrunRoot));
        if (Directory.GetCurrentDirectory() != kestrunRoot)
        {
            Directory.SetCurrentDirectory(kestrunRoot);
        }
        KestrunRoot = kestrunRoot;
    }
    public static KestrunHost Create(string name, Func<KestrunHost> factory, bool setAsDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Instance name is required.", nameof(name));

        if (_instances.ContainsKey(name))
            throw new InvalidOperationException($"A KestrunHost instance with the name '{name}' already exists.");

        var host = factory();
        _instances[name] = host;

        if (setAsDefault || _defaultName == null)
            _defaultName = name;

        return host;
    }
    public static KestrunHost Create(string name,
         string[]? modulePathsObj = null, bool setAsDefault = false)
    {
        // Call the overload with a default logger (null or a default instance as appropriate)
        return Create(name, Log.Logger, modulePathsObj, setAsDefault);
    }

    public static KestrunHost Create(string name, Serilog.ILogger logger,
         string[]? modulePathsObj = null, bool setAsDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Instance name is required.", nameof(name));

        if (_instances.ContainsKey(name))
            throw new InvalidOperationException($"A KestrunHost instance with the name '{name}' already exists.");

        if (KestrunRoot is null)
            throw new InvalidOperationException("Kestrun root path must be set before creating a KestrunHost instance.");

        var host = new KestrunHost(name, logger, KestrunRoot, modulePathsObj);
        _instances[name] = host;

        if (setAsDefault || _defaultName == null)
            _defaultName = name;

        return host;
    }


    public static bool TryGet(string name, out KestrunHost? host) =>
        _instances.TryGetValue(name, out host);

    public static KestrunHost? Get(string name) =>
        _instances.TryGetValue(name, out var host) ? host : null;

    public static KestrunHost? Default => _defaultName != null && _instances.TryGetValue(_defaultName, out var host) ? host : null;

    public static void SetDefault(string name)
    {
        if (!_instances.ContainsKey(name))
            throw new InvalidOperationException($"No KestrunHost instance named '{name}' exists.");

        _defaultName = name;
    }

    public static async Task StartAsync(string name, CancellationToken ct = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Starting (Async) KestrunHost instance '{Name}'", name);
        if (TryGet(name, out var host))
        {
            if (host is not null)
            {
                await host.StartAsync(ct);
            }
            else
            {
                throw new InvalidOperationException($"KestrunHost instance '{name}' is null.");
            }
        }
        else
        {
            throw new InvalidOperationException($"No KestrunHost instance named '{name}' exists.");
        }
    }

    public static async Task StopAsync(string name, CancellationToken ct = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Stopping (Async) KestrunHost instance '{Name}'", name);
        if (TryGet(name, out var host))
        {
            if (host is not null)
            {
                await host.StopAsync(ct);
            }
            else
            {
                throw new InvalidOperationException($"KestrunHost instance '{name}' is null.");
            }
        }
        else
        {
            throw new InvalidOperationException($"No KestrunHost instance named '{name}' exists.");
        }
    }

    public static async Task StopAllAsync(CancellationToken ct = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Stopping all KestrunHost instances (Async)");
        foreach (var kv in _instances)
        {
            await kv.Value.StopAsync(ct);
        }
    }

    public static void Destroy(string name)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Destroying KestrunHost instance '{Name}'", name);
        if (_instances.TryRemove(name, out var host))
        {
            host.Dispose();
            if (_defaultName == name)
                _defaultName = _instances.Keys.FirstOrDefault();
        }
    }

    public static void DestroyAll()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Destroying all KestrunHost instances");
        foreach (var name in _instances.Keys.ToList())
        {
            Destroy(name);
        }
        _defaultName = null;
        Log.CloseAndFlush();
    }


}
