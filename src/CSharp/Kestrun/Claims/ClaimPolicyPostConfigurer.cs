using Kestrun.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Kestrun.Claims;

/// <summary>
/// Copies <see cref="ClaimPolicyConfig"/> into <see cref="AuthorizationOptions"/> after configuration.
/// </summary>
public sealed class ClaimPolicyPostConfigurer
    : IPostConfigureOptions<AuthorizationOptions>
{
    private readonly string _scheme;
    private readonly IOptionsMonitor<IClaimsCommonOptions> _basics;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimPolicyPostConfigurer"/> class.
    /// </summary>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="basics">The options monitor for <see cref="BasicAuthenticationOptions"/>.</param>
    public ClaimPolicyPostConfigurer(
        string scheme,
        IOptionsMonitor<IClaimsCommonOptions> basics)
    {
        _scheme = scheme;
        _basics = basics;
    }

    /// <summary>
    /// Applies the <see cref="ClaimPolicyConfig"/> to the specified <see cref="AuthorizationOptions"/> after configuration.
    /// </summary>
    /// <param name="name">The name of the options instance being configured. May be null.</param>
    /// <param name="authz">The <see cref="AuthorizationOptions"/> instance to configure.</param>
    public void PostConfigure(string? name, AuthorizationOptions authz)
    {
        // Only run once for the default options instance
        var claimCfg = _basics.Get(_scheme).ClaimPolicyConfig;
        claimCfg?.ToAuthzDelegate()?.Invoke(authz);
    }
}
