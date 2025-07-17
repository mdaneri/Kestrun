namespace KestrumLib
{
    /// <summary>
    /// Minimal <see cref="IHostLifetime"/> implementation used when hosting inside another application.
    /// </summary>
    public class NoopHostLifetime : IHostLifetime
    {
        /// <inheritdoc />
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}