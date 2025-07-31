using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using YamlDotNet.Serialization;
namespace Kestrun.Security;

public sealed class JwtBuilderResult
{
    private readonly string _token;
    private readonly SymmetricSecurityKey? _key;
    private readonly JwtTokenBuilder _builder;
    private TimeSpan _expires;

    public DateTime IssuedAt { get; }
    public DateTime Expires { get;private set; }

    public JwtBuilderResult(
        string token,
        SymmetricSecurityKey? key,
        JwtTokenBuilder builder,
        DateTime issuedAt,
        DateTime expires)
    {
        _token = token;
        _key = key;
        _builder = builder;
        IssuedAt = issuedAt;
        Expires = expires;
    }

    /// <summary>Get the JWT compact string.</summary>
    public string Token() => _token;


    public TokenValidationParameters ValidationParameters(TimeSpan? clockSkew = null)
    {
        var tvp = new TokenValidationParameters
        {
            ValidateIssuer = _builder.Issuer is not null,
            ValidIssuer = _builder.Issuer,
            ValidateAudience = _builder.Audience is not null,
            ValidAudience = _builder.Audience,
            ValidateLifetime = true,
            ClockSkew = clockSkew ?? TimeSpan.FromMinutes(1),

            RequireSignedTokens = _key is not null,
            ValidateIssuerSigningKey = _key is not null,
            IssuerSigningKey = _key,
            ValidAlgorithms = _builder.Algorithm != null ? [_builder.Algorithm] : []
        };
        return tvp;
    }

    /// <summary>Re-issue a fresh token with the same claims/iss/aud and new TTL.</summary>
    public string Renew(TimeSpan? lifetime = null)
    {
        if (_key is null)
            throw new InvalidOperationException("Cannot renew: no symmetric key available.");
        return RenewAsync(_token, _key, lifetime).GetAwaiter().GetResult();
    }
    public async Task<string> RenewAsync(TimeSpan? lifetime = null)
    {
        if (_key is null)
            throw new InvalidOperationException("Cannot renew: no symmetric key available.");
        return await RenewAsync(_token, _key, lifetime);
    }


    private async Task<string> RenewAsync(string jwt, SymmetricSecurityKey signingKey, TimeSpan? lifetime = null)
    {
        var handler = new JsonWebTokenHandler();      // supports JWE & JWS

        // ①  quick validation (signature only; ignore exp, aud, iss)
        var result = await handler.ValidateTokenAsync(
            jwt,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,

                ValidateLifetime = false,     // allow even expired token
                ValidateAudience = false,
                ValidateIssuer = false
            });

        if (!result.IsValid)
            throw new SecurityTokenException(
                $"Input token not valid: {result.Exception?.Message}");

        var orig = (JsonWebToken)result.SecurityToken!;
        _expires  = lifetime ?? (orig.ValidTo - orig.ValidFrom);

        return _builder.ValidFor(_expires).Build().Token();     
    }
    public   string Renew(string jwt, SymmetricSecurityKey signingKey, TimeSpan? lifetime = null)
    {
        return RenewAsync(jwt, signingKey, lifetime).GetAwaiter().GetResult();
    }
}
