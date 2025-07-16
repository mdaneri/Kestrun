using System.Management.Automation.Runspaces;
using static KestrumLib.KestrunHost;
namespace KestrumLib
{
    public static class PowerShellRunspaceMiddlewareExtensions
    {
        public static IApplicationBuilder UsePowerShellRunspace(
            this IApplicationBuilder app, RunspacePool pool)
        {
            return app.UseMiddleware<PowerShellRunspaceMiddleware>(pool);
        }
    }
}