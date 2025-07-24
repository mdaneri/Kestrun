namespace Kestrun.Scheduling;

using System;
using System.Threading;
using System.Threading.Tasks;
using Cronos;

internal sealed record ScheduledTask(
    string                Name,
    Func<CancellationToken, Task> Work,
    CronExpression?       Cron,
    TimeSpan?             Interval,
    bool                  RunImmediately,
    CancellationTokenSource TokenSource
);
