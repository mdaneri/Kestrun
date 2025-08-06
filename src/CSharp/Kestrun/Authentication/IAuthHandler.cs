


using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

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
}