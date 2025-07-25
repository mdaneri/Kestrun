namespace Kestrun;

/// <summary>
/// Minimal <see cref="IHostLifetime"/> that performs no blocking operations.
/// Useful for scenarios like testing where the host lifecycle is externally managed.
/// </summary>
public class NoopHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
