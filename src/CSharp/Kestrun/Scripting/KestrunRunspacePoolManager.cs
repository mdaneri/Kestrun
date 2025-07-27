using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using Serilog;
using Serilog.Events;

namespace Kestrun.Scripting;

public sealed class KestrunRunspacePoolManager : IDisposable
{
    private readonly ConcurrentBag<Runspace> _stash = new();
    private readonly InitialSessionState _iss;
    private readonly int _max;
    private int _count;        // total live runspaces
    private bool _disposed;

    public int MinRunspaces { get; }
    public int MaxRunspaces => _max;

    /// <summary>
    /// Thread‑affinity strategy for *future* runspaces.
    /// Default is <see cref="PSThreadOptions.ReuseThread"/>.
    /// </summary>
    public PSThreadOptions ThreadOptions { get; set; } = PSThreadOptions.ReuseThread;

    // ───────────────── constructor ──────────────────────────
    public KestrunRunspacePoolManager(
        int minRunspaces,
        int maxRunspaces,
        InitialSessionState? initialSessionState = null,
        PSThreadOptions threadOptions = PSThreadOptions.ReuseThread)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Initializing RunspacePoolManager: Min={Min}, Max={Max}", minRunspaces, maxRunspaces);

        ArgumentOutOfRangeException.ThrowIfNegative(minRunspaces);
        if (maxRunspaces < 1 || maxRunspaces < minRunspaces)
            throw new ArgumentOutOfRangeException(nameof(maxRunspaces));

        MinRunspaces = minRunspaces;
        _max = maxRunspaces;
        _iss = initialSessionState ?? InitialSessionState.CreateDefault();
        ThreadOptions = threadOptions;

        // warm the stash
        for (int i = 0; i < minRunspaces; i++)
            _stash.Add(CreateRunspace());
        _count = minRunspaces;
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Warm-started pool with {Count} runspaces", _count);
    }

    // ───────────────── public API ────────────────────────────
    /// <summary>Borrow a runspace (creates one if under the cap).</summary>
    public Runspace Acquire()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Acquiring runspace from pool: CurrentCount={Count}, Max={Max}", _count, _max);

        ObjectDisposedException.ThrowIf(_disposed, nameof(KestrunRunspacePoolManager));

        if (_stash.TryTake(out var rs))
        {
            if (rs.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                Log.Warning("Runspace from stash is not opened: {State}. Discarding and acquiring a new one.", rs.RunspaceStateInfo.State);
                // If the runspace is not open, we cannot use it.
                // Discard and try again
                rs.Dispose();
                Interlocked.Decrement(ref _count);
                return Acquire();
            }
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Reusing runspace from stash: StashCount={Count}", _stash.Count);
            return rs;
        }
        // Need a new one?—but only if we haven’t reached max.
        if (Interlocked.Increment(ref _count) <= _max)
        {
            Log.Debug("Creating new runspace: TotalCount={Count}", _count);
            return CreateRunspace();
        }
        // Overshot: roll back and complain.
        Interlocked.Decrement(ref _count);

        Log.Warning("Runspace limit reached: Max={Max}", _max);
        throw new InvalidOperationException("Run-space limit reached.");
    }

    /// <summary>
    /// Asynchronously borrow a runspace (waits if none available and at max).
    /// </summary>
    public async Task<Runspace> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Acquiring runspace (async) from pool: CurrentCount={Count}, Max={Max}", _count, _max);

        while (true)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KestrunRunspacePoolManager));

            if (_stash.TryTake(out var rs))
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                    Log.Debug("Reusing runspace from stash (async): StashCount={Count}", _stash.Count);
                return rs;
            }

            if (Interlocked.Increment(ref _count) <= _max)
            {
                Log.Debug("Creating new runspace (async): TotalCount={Count}", _count);
                // Runspace creation is synchronous, but we can offload to thread pool
                return await Task.Run(() => CreateRunspace(), cancellationToken).ConfigureAwait(false);
            }
            Interlocked.Decrement(ref _count);

            // Wait for a runspace to be returned
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Waiting for runspace to become available (async)");

            // Use a short delay to poll for availability
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Return the runspace so the pool can reuse it.</summary>
    public void Release(Runspace rs)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Release() called: Disposed={Disposed}", _disposed);

        if (_disposed)
        {
            Log.Warning("Pool disposed; disposing returned runspace");
            rs.Dispose();
            return;
        }

        _stash.Add(rs);
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Runspace returned to stash: StashCount={Count}", _stash.Count);
        // Note: we do not decrement _count here, as the pool size is fixed.
        // The pool will keep the runspace open for reuse.
    }


    // ───────────────── helpers ───────────────────────────────
    private Runspace CreateRunspace()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("CreateRunspace() - creating new runspace");

        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Creating new runspace with InitialSessionState: {Iss}", _iss);
        var rs = RunspaceFactory.CreateRunspace(_iss);

        // Apply the chosen thread‑affinity strategy **before** opening.
        rs.ThreadOptions = ThreadOptions;
        rs.ApartmentState = ApartmentState.MTA;     // always MTA 

        rs.Open();
        Log.Information("Opened new Runspace with ThreadOptions={ThreadOptions}", ThreadOptions);
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("New runspace created: {Runspace}", rs);

        return rs;
    }

    // ───────────────── cleanup ───────────────────────────────
    public void Dispose()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Disposing KestrunRunspacePoolManager: Disposed={Disposed}", _disposed);
        if (_disposed) return;
        _disposed = true;
        Log.Information("Disposing RunspacePoolManager and all pooled runspaces");
        while (_stash.TryTake(out var rs))
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Disposing runspace: {Runspace}", rs);
            try { rs.Close(); } catch { /* ignore */ }
            rs.Dispose();
        }
        Log.Information("RunspacePoolManager disposed");
    }
}
