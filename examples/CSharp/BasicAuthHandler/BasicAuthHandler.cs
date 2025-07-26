using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private static readonly Serilog.ILogger Log =
        Serilog.Log.ForContext<BasicAuthHandler>();
    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,          // <-- ask DI for this
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization Header"));

        try
        {
            var authorizationHeader = Request.Headers.Authorization;
            if (string.IsNullOrEmpty(authorizationHeader))
                return Task.FromResult(AuthenticateResult.Fail("Missing Authorization Header"));
            var authHeader = AuthenticationHeaderValue.Parse(authorizationHeader.ToString());
            var creds = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter ?? ""))
                                      .Split(':', 2);
            if (creds[0] == "admin" && creds[1] == "s3cr3t")
            {
                var claims = new[] { new Claim(ClaimTypes.Name, creds[0]) };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            return Task.FromResult(AuthenticateResult.Fail("Invalid username or password"));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));
        }
    }
}
