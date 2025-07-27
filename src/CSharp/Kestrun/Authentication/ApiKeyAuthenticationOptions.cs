
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Kestrun.Authentication;
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{    
    /// <summary>
    /// Name of the header to look for the API key.
    /// </summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// Name of the authentication scheme.
    /// </summary>
    public string SchemeName { get; set; } = "ApiKey";

    /// <summary>
    /// Other headers to try if the primary one is missing.
    /// <para>Defaults to empty.</para>
    /// <para>Use this to support multiple header names for the API key.</para>
    /// </summary>
    public string[] AdditionalHeaderNames { get; set; } = [];

    /// <summary>
    /// If true, also look for the key in the query string.
    /// <para>Defaults to false.</para>
    /// <para>Note: this is less secure, as query strings can be logged.</para>
    /// <para>Use with caution.</para>
    /// </summary>
    public bool AllowQueryStringFallback { get; set; } = false;

    /// <summary>
    /// Single expected API key (used if ValidateKey is not set).
    /// <para>Defaults to null.</para>
    /// <para>Use this for simple scenarios where you have a known key.</para>
    /// </summary>
    public string? ExpectedKey { get; set; }

    /// <summary>
    /// Called to validate the raw key string. Return true if valid.
    /// <para>This is called for every request, so it should be fast.</para>
    /// </summary>
    public Func<string, bool> ValidateKey { get; set; } = _ => false;

    /// <summary>
    /// Logger for this authentication scheme.
    /// <para>Defaults to Serilog's global logger.</para>
    /// </summary>
    public Serilog.ILogger Logger { get; set; } = Serilog.Log.ForContext<ApiKeyAuthenticationOptions>();

    /// <summary>
    /// If true, requires HTTPS for API key requests.
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// After credentials are valid, this is called to add extra Claims.
    /// Parameters: HttpContext, username → IEnumerable of extra claims.
    /// </summary>
    public Func<HttpContext, string, IEnumerable<Claim>>? IssueClaims { get; set; }

    /// <summary>
    /// If provided, returns the username associated with a given API key.
    /// Used to populate ClaimTypes.Name.
    /// </summary>
    public Func<string, string>? ResolveUsername { get; set; }

    /// <summary>
    /// If true, includes the <c>WWW-Authenticate</c> header in 401 responses.
    /// <para>Default: <c>true</c>.</para>
    /// <para>Set to <c>false</c> to suppress automatic hints to clients.</para>
    /// </summary>
    public bool EmitChallengeHeader { get; set; } = true;

    /// <summary>
    /// Format for the <c>WWW-Authenticate</c> header in 401 responses.
    /// <para>
    /// If set to <c>ApiKeyHeader</c>, emits <c>ApiKey header="X-Api-Key"</c>.
    /// If set to <c>HeaderOnly</c>, emits just the header name.
    /// </para>
    /// </summary>
    public ApiKeyChallengeFormat ChallengeHeaderFormat { get; set; } = ApiKeyChallengeFormat.ApiKeyHeader;

}