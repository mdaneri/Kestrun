using System.Management.Automation.Runspaces;
using static Kestrun.KestrunHost;
namespace Kestrun;

/// <summary>
/// Extension methods for adding PowerShell runspace middleware.
/// </summary>
public static class PowerShellRunspaceMiddlewareExtensions
{
    /// <summary>
    /// Registers <see cref="PowerShellRunspaceMiddleware"/> with the given runspace pool.
    /// </summary>
    public static IApplicationBuilder UsePowerShellRunspace(
        this IApplicationBuilder app, KestrunRunspacePoolManager pool)
    {
        return app.UseMiddleware<PowerShellRunspaceMiddleware>(pool);
    }
}
