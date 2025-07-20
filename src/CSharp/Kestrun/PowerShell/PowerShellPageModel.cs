// Kestrun -– shared PageModel for PS-driven Razor views
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace Kestrun;

/// <summary>
/// Base PageModel that exposes whatever the sibling PowerShell script placed
/// in <c>HttpContext.Items["PageModel"]</c>.
/// </summary>
public class PowerShellPageModel : PageModel
{
    /// <summary>The dynamic object assigned to <c>$Model</c> in PowerShell.</summary>
    public dynamic? Data => HttpContext.Items["PageModel"];

    // convenience helpers ----------------------------------------------
    public string? Query(string key) => HttpContext.Request.Query[key];

    public IConfiguration Config =>
        HttpContext.RequestServices.GetRequiredService<IConfiguration>();
}
