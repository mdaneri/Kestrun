using System.Management.Automation.Runspaces;
using static Kestrun.KestrunHost;
namespace Kestrun
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