using System.Management.Automation.Runspaces;
using static KestrumLib.KestrunHost;
namespace KestrumLib
{
    /// <summary>
    /// Extension methods for adding PowerShell runspace middleware to the ASP.NET pipeline.
    /// </summary>
    public static class PowerShellRunspaceMiddlewareExtensions
    {
        /// <summary>
        /// Registers middleware that provides a pooled PowerShell runspace to each request.
        /// </summary>
        /// <param name="app">Application builder.</param>
        /// <param name="pool">Runspace pool to use for script execution.</param>
        public static IApplicationBuilder UsePowerShellRunspace(
            this IApplicationBuilder app, RunspacePool pool)
        {
            return app.UseMiddleware<PowerShellRunspaceMiddleware>(pool);
        }
    }
}