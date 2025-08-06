


using System.Management.Automation;
using System.Security.Claims;
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

        if (Options.NativeIssueClaims is not null)
        {
            // Call the NativeIssueClaims function to get additional claims
            var extra = Options.NativeIssueClaims(Context, user);
            if (extra is not null)
                claims.AddRange(extra);
        }
        // Create the ClaimsIdentity and ClaimsPrincipal
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        // Create the AuthenticationTicket with the principal and scheme name
        var principal = new ClaimsPrincipal(identity);
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
}