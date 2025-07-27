using System.Security.Claims;
using System.Text.Encodings.Web;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Kestrun.Authentication;

public class ApiKeyAuthHandler
    : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (Options.RequireHttps && !Request.IsHttps)
                return Fail("HTTPS required");

            // ① Try the primary header
            if (!Request.Headers.TryGetValue(Options.HeaderName, out var values))
            {
                // ② Then try any additional headers
                foreach (var header in Options.AdditionalHeaderNames)
                {
                    if (Request.Headers.TryGetValue(header, out values))
                        break;
                }
            }

            // ③ Finally, if still missing & fallback is allowed, check the query string
            if ((values.Count == 0 || StringValues.IsNullOrEmpty(values))
                && Options.AllowQueryStringFallback
                && Request.Query.TryGetValue(Options.HeaderName, out var qsValues))
            {
                values = qsValues;
            }

            // ④ If we still have nothing, fail
            if (StringValues.IsNullOrEmpty(values))
                return Fail("Missing API Key");

            var providedKey = values.ToString();

            // ⑤ Now validate
            bool valid = false;
            if (Options.ValidateKey is not null)
            {
                valid = Options.ValidateKey(providedKey);
            }
            else if (!string.IsNullOrEmpty(Options.ExpectedKey))
            {
                valid = SecurityUtilities.FixedTimeEquals(providedKey, Options.ExpectedKey);
            }
            else
            {
                throw new InvalidOperationException(
                    "No API key validation configured. " +
                    "Either set ValidateKey or ExpectedKey in ApiKeyAuthenticationOptions.");
            }

            if (!valid)
            {
                return Fail($"Invalid API Key: {providedKey}");
            }
            var username = Options.ResolveUsername?.Invoke(providedKey) ?? "ApiKeyClient";

            var claims = new List<Claim> { new(ClaimTypes.Name, username) };

            // If the consumer wired up IssueClaims, invoke it now:
            if (Options.IssueClaims is not null)
            {
                var extra = Options.IssueClaims(Context, username);
                if (extra is not null)
                    claims.AddRange(extra);
            }
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            Options.Logger.Information("API Key authentication succeeded for identity: {Username}", username);

            // ⑥ Return success
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Options.Logger.Error(ex, "API Key authentication failed with exception.");
            return Fail("Error processing API Key");
        }
    }
    Task<AuthenticateResult> Fail(string reason)
    {
        Options.Logger.Warning("API Key authentication failed: {Reason}", reason);
        return Task.FromResult(AuthenticateResult.Fail(reason));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Options.EmitChallengeHeader)
        {
            var header = Options.HeaderName ?? "X-Api-Key";
            var value = Options.ChallengeHeaderFormat switch
            {
                ApiKeyChallengeFormat.HeaderOnly => header,
                _ => $"ApiKey header=\"{header}\""
            };

            Response.Headers.WWWAuthenticate = value;
        }
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

}