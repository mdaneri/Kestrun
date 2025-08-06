


using System.Collections;
using System.Management.Automation;
using System.Security.Claims;
using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Models;
using Kestrun.SharedState;
using Microsoft.AspNetCore.Authentication;
using Serilog;

namespace Kestrun.Authentication;


/// <summary>
/// Defines common options for authentication, including code validation, claim issuance, and claim policy configuration.
/// </summary>
public interface IAuthHandler
{


    /// <summary>
    /// Generates an <see cref="AuthenticationTicket"/> for the specified user and authentication scheme, issuing additional claims as configured.
    /// </summary>
    /// <param name="Context">The current HTTP context.</param>
    /// <param name="user">The user name for whom the ticket is being generated.</param>
    /// <param name="Options">Authentication options including claim issuance delegates.</param>
    /// <param name="Scheme">The authentication scheme to use.</param>
    /// <returns>An <see cref="AuthenticationTicket"/> representing the authenticated user.</returns>
    public static async Task<AuthenticationTicket> GetAuthenticationTicketAsync(
        HttpContext Context, string user,
    IAuthenticationCommonOptions Options, AuthenticationScheme Scheme)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, user) };

        // If the consumer wired up IssueClaims, invoke it now:
        if (Options.IssueClaims is not null)
        {
            // Call the IssueClaims function to get additional claims
            var extra = await Options.IssueClaims(Context, user);
            if (extra is not null)
            {
                foreach (var claim in extra)
                {
                    if (claim is not null && !string.IsNullOrEmpty(claim.Value) && claim is Claim)
                        claims.Add(claim);
                }
            }
        } 
        // Create the ClaimsIdentity and ClaimsPrincipal
        var claimsIdentity = new ClaimsIdentity(claims, Scheme.Name);
        // Create the AuthenticationTicket with the principal and scheme name
        var principal = new ClaimsPrincipal(claimsIdentity);
        // Create the authentication ticket
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return ticket;
    }



    /// <summary>
    /// Authenticates the user using PowerShell script.
    /// This method is used to validate the username and password against a PowerShell script.
    /// </summary>
    /// <param name="code">The PowerShell script code used for authentication.</param>
    /// <param name="context">The HTTP context.</param>
    /// <param name="credentials">A dictionary containing the credentials to validate (e.g., username and password).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async ValueTask<bool> ValidatePowerShellAsync(string? code, HttpContext context, Dictionary<string, string> credentials)
    {
        try
        {
            if (!context.Items.ContainsKey("PS_INSTANCE"))
            {
                throw new InvalidOperationException("PowerShell runspace not found in context items. Ensure PowerShellRunspaceMiddleware is registered.");
            }
            // Validate that the credentials dictionary is not null or empty
            if (credentials == null || credentials.Count == 0)
            {
                Log.Warning("Credentials are null or empty.");
                return false;
            }
            // Validate that the code is not null or empty
            if (string.IsNullOrEmpty(code))
            {
                throw new InvalidOperationException("PowerShell authentication code is null or empty.");
            }
            // Retrieve the PowerShell instance from the context
            PowerShell ps = context.Items["PS_INSTANCE"] as PowerShell
                  ?? throw new InvalidOperationException("PowerShell instance not found in context items.");
            if (ps.Runspace == null)
            {
                throw new InvalidOperationException("PowerShell runspace is not set. Ensure PowerShellRunspaceMiddleware is registered.");
            }

            ps.AddScript(code, useLocalScope: true);
            foreach (var kvp in credentials)
            {
                ps.AddParameter(kvp.Key, kvp.Value);
            }

            var psResults = await ps.InvokeAsync().ConfigureAwait(false);

            if (psResults.Count == 0 || psResults[0] == null || psResults[0].BaseObject is not bool isValid)
            {
                Log.Error("PowerShell script did not return a valid boolean result.");
                return false;
            }
            return isValid;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during validating PowerShell authentication.");
            return false;
        }
    }

    internal static Func<HttpContext, IDictionary<string, object?>, Task<bool>> BuildCsValidator(
        AuthenticationCodeSettings settings,
        Serilog.ILogger log,
          params (string Name, object? Prototype)[] globals)
    {
        if (settings is null)
            throw new ArgumentNullException(nameof(settings), "AuthenticationCodeSettings cannot be null");
        // Place-holders so Roslyn knows the globals that will exist
        var stencil = globals.ToDictionary(n => n.Name, n => n.Prototype,
                                                 StringComparer.OrdinalIgnoreCase);

        var script = CSharpDelegateBuilder.Compile(
            settings.Code,
            log,                             // already scoped by caller
            settings.ExtraImports,
            settings.ExtraRefs,
            stencil,
            languageVersion: settings.CSharpVersion);

        // Return the runtime delegate
        return async (ctx, vars) =>
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                Log.Debug("Running C# authentication script with variables: {Variables}", vars);
            // --- Kestrun plumbing -------------------------------------------------
            var krReq = await KestrunRequest.NewRequest(ctx);
            var krRes = new KestrunResponse(krReq);
            var kCtx = new KestrunContext(krReq, krRes, ctx);
            // ---------------------------------------------------------------------
            var globalsDict = new Dictionary<string, object?>(
                    vars, StringComparer.OrdinalIgnoreCase);
            // Merge shared state + user variables
            var globals = new CsGlobals(
                SharedStateStore.Snapshot(),
                kCtx,
                globalsDict);

            var result = await script.RunAsync(globals).ConfigureAwait(false);
            return result.ReturnValue is true;
        };
    }

    internal static Func<HttpContext, IDictionary<string, object?>, Task<bool>> BuildVBNetValidator(
        AuthenticationCodeSettings settings,
        Serilog.ILogger log,
        params string[] variableNames)
    {
        if (settings is null)
            throw new ArgumentNullException(nameof(settings), "AuthenticationCodeSettings cannot be null");
        // Place-holders so Roslyn knows the globals that will exist
        var stencil = variableNames.ToDictionary(n => n, n => (object?)null,
                                                 StringComparer.OrdinalIgnoreCase);

        var script = VBNetDelegateBuilder.Compile<bool>(
            settings.Code,
            log,                             // already scoped by caller
            settings.ExtraImports,
            settings.ExtraRefs,
            stencil,
            languageVersion: settings.VisualBasicVersion);

        // Return the runtime delegate
        return async (ctx, vars) =>
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                Log.Debug("Running VB.NET authentication script with variables: {Variables}", vars);

            // --- Kestrun plumbing -------------------------------------------------
            var krReq = await KestrunRequest.NewRequest(ctx);
            var krRes = new KestrunResponse(krReq);
            var kCtx = new KestrunContext(krReq, krRes, ctx);
            // ---------------------------------------------------------------------

            // Merge shared state + user variables
            var globals = new CsGlobals(
                SharedStateStore.Snapshot(),
                kCtx,
                new Dictionary<string, object?>(vars, StringComparer.OrdinalIgnoreCase));

            var result = await script(globals).ConfigureAwait(false);

            if (result is bool isValid)
            {
                return isValid;
            }
            return false;
        };
    }


/// <summary>
    /// Builds a PowerShell-based function for issuing claims for a user.
    /// </summary>
    /// <param name="settings">The authentication code settings containing the PowerShell script.</param>
    /// <returns>A function that issues claims using the provided PowerShell script.</returns>
    public static Func<HttpContext, string, Task<IEnumerable<Claim>>> BuildPsIssueClaims(AuthenticationCodeSettings settings)
  => async (ctx, identity) =>
        {
            return await IAuthHandler.IssueClaimsPowerShellAsync(settings.Code, ctx, identity);
        };
    /// <summary>
    /// Issues claims for a user by executing a PowerShell script.
    /// </summary>
    /// <param name="code">The PowerShell script code used to issue claims.</param>
    /// <param name="ctx">The HTTP context containing the PowerShell runspace.</param>
    /// <param name="identity">The username for which to issue claims.</param>
    /// <returns>A task representing the asynchronous operation, with a collection of issued claims.</returns>
    public static async Task<IEnumerable<Claim>> IssueClaimsPowerShellAsync(string? code, HttpContext ctx, string identity)
    {
        try
        {
            if (!ctx.Items.ContainsKey("PS_INSTANCE"))
            {
                throw new InvalidOperationException("PowerShell runspace not found in context items. Ensure PowerShellRunspaceMiddleware is registered.");
            }

            if (string.IsNullOrWhiteSpace(identity))
            {
                Log.Warning("Identity is null or empty.");
                return [];
            }
            if (string.IsNullOrEmpty(code))
            {
                throw new InvalidOperationException("PowerShell authentication code is null or empty.");
            }

            PowerShell ps = ctx.Items["PS_INSTANCE"] as PowerShell
                  ?? throw new InvalidOperationException("PowerShell instance not found in context items.");
            if (ps.Runspace == null)
            {
                throw new InvalidOperationException("PowerShell runspace is not set. Ensure PowerShellRunspaceMiddleware is registered.");
            }


            ps.AddScript(code, useLocalScope: true)
            .AddParameter("identity", identity);
            var psResults = await ps.InvokeAsync().ConfigureAwait(false);

            var claims = new List<Claim>();
            foreach (var r in psResults)
            {
                var obj = r.BaseObject;
                switch (obj)
                {
                    // ① Script returned a Claim object
                    case Claim c:
                        claims.Add(c);
                        break;

                    // ② Script returned a PSCustomObject or hashtable with Type/Value
                    case IDictionary dict when dict.Contains("Type") && dict.Contains("Value"):
                        var typeObj = dict["Type"];
                        var valueObj = dict["Value"];
                        if (typeObj is not null && valueObj is not null)
                        {
                            var typeStr = typeObj.ToString();
                            var valueStr = valueObj.ToString();
                            if (!string.IsNullOrEmpty(typeStr) && !string.IsNullOrEmpty(valueStr))
                            {
                                claims.Add(new Claim(typeStr, valueStr));
                            }
                        }
                        break;

                    // ③ Script returned "type:value"
                    case string s when s.Contains(':'):
                        var idx = s.IndexOf(':');
                        claims.Add(new Claim(s[..idx], s[(idx + 1)..]));
                        break;
                }
            }
            return claims;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Issue Claims for {Identity}", identity);
            return [];
        }
    }


     /// <summary>
    /// Builds a C#-based function for issuing claims for a user.
    /// </summary>
    /// <param name="settings">The authentication code settings containing the C# script.</param>
    /// <returns>A function that issues claims using the provided C# script.</returns>
    public static Func<HttpContext, string, Task<IEnumerable<Claim>>> BuildCsIssueClaims(AuthenticationCodeSettings settings)
    {
        var script = CSharpDelegateBuilder.Compile(settings.Code, Serilog.Log.ForContext<BasicAuthHandler>(),
        settings.ExtraImports, settings.ExtraRefs,
        new Dictionary<string, object?>
            {
                { "identity", "" }
            }, languageVersion: settings.CSharpVersion);

        return async (ctx, identity) =>
        {
            var krRequest = await KestrunRequest.NewRequest(ctx);
            var krResponse = new KestrunResponse(krRequest);
            var context = new KestrunContext(krRequest, krResponse, ctx);
            var globals = new CsGlobals(SharedStateStore.Snapshot(), context, new Dictionary<string, object?>
            {
                { "identity", identity }
            });
            var result = await script.RunAsync(globals).ConfigureAwait(false);
            return result.ReturnValue is IEnumerable<Claim> claims
                ? claims
                : [];
        };
    }


    /// <summary>
    /// Builds a VB.NET-based function for issuing claims for a user.
    /// </summary>
    /// <param name="settings">The authentication code settings containing the VB.NET script.</param>
    /// <returns>A function that issues claims using the provided VB.NET script.</returns>
    public static Func<HttpContext, string, Task<IEnumerable<Claim>>> BuildVBNetIssueClaims(AuthenticationCodeSettings settings)
    {
        var script = VBNetDelegateBuilder.Compile<IEnumerable<Claim>>(settings.Code, Serilog.Log.ForContext<BasicAuthHandler>(),
        settings.ExtraImports, settings.ExtraRefs,
        new Dictionary<string, object?>
            {
                { "identity", "" }
            }, languageVersion: settings.VisualBasicVersion);

        return async (ctx, identity) =>
        {
            var krRequest = await KestrunRequest.NewRequest(ctx);
            var krResponse = new KestrunResponse(krRequest);
            var context = new KestrunContext(krRequest, krResponse, ctx);
            var glob = new CsGlobals(SharedStateStore.Snapshot(), context, new Dictionary<string, object?>
            {
                { "identity", identity }
            });
            // Run the VB.NET script and get the result
            // Note: The script should return a boolean indicating success or failure
            var result = await script(glob).ConfigureAwait(false);
            return result is IEnumerable<Claim> claims
              ? claims
           : [];

        };
    }
}