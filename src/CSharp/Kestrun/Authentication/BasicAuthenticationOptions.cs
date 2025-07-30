using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;

namespace Kestrun.Authentication;

public partial class BasicAuthenticationOptions : AuthenticationSchemeOptions, ICloneable
{
    /// <summary>HTTP header to read the key from.</summary>
    /// <remarks>Defaults to "Authorization".</remarks>
    public string HeaderName { get; set; } = "Authorization";

    /// <summary>Called to validate the raw key string. Return true if valid.</summary>
    public Func<HttpContext, string, string, Task<bool>> ValidateCredentials { get; set; } = (context, username, password) => Task.FromResult(false);

    public bool Base64Encoded { get; set; } = true;

    public Regex SeparatorRegex { get; set; } = MyRegex();


    public string Realm { get; set; } = "Kestrun";

    [GeneratedRegex("^([^:]*):(.*)$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    public Serilog.ILogger Logger { get; set; } = Serilog.Log.ForContext<BasicAuthenticationOptions>();

    public bool RequireHttps { get; set; } = true;

    public bool SuppressWwwAuthenticate { get; set; }

    /// <summary>
    /// After credentials are valid, this is called to add extra Claims.
    /// Parameters: HttpContext, username → IEnumerable of extra claims.
    /// </summary>
    public Func<HttpContext, string, IEnumerable<Claim>>? IssueClaims { get; set; }


    /// <summary>
    /// Settings for the authentication code, if using a script.
    /// </summary>
    /// <remarks>
    /// This allows you to specify the language, code, and additional imports/refs.
    /// </remarks>
    public AuthenticationCodeSettings CodeSettings { get; set; } = new();

     public object Clone()
    {
        return new BasicAuthenticationOptions
        {
            HeaderName = this.HeaderName,
            ValidateCredentials = this.ValidateCredentials,
            Base64Encoded = this.Base64Encoded,
            SeparatorRegex = new Regex(this.SeparatorRegex.ToString(), this.SeparatorRegex.Options),
            Realm = this.Realm,
            Logger = this.Logger,
            RequireHttps = this.RequireHttps,
            SuppressWwwAuthenticate = this.SuppressWwwAuthenticate,
            IssueClaims = this.IssueClaims,
            CodeSettings = this.CodeSettings is not null ? this.CodeSettings.Copy() : new AuthenticationCodeSettings()
        };
    }
}

