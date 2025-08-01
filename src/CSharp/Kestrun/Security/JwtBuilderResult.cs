using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using YamlDotNet.Serialization;
namespace Kestrun.Security;

/// <summary>
/// Represents the result of building a JWT, including the token, key, builder, issue time, and expiration.
/// </summary>
/// <param name="token">The JWT compact string.</param>
/// <param name="key">The symmetric security key used for signing.</param>
/// <param name="builder">The JWT token builder instance.</param>
/// <param name="issuedAt">The time at which the token was issued.</param>
/// <param name="expires">The expiration time of the token.</param>
public sealed class JwtBuilderResult(
    string token,
    SymmetricSecurityKey? key,
    JwtTokenBuilder builder,
    DateTime issuedAt,
    DateTime expires)
{
    private readonly string _token = token;
    private readonly SymmetricSecurityKey? _key = key;
    private readonly JwtTokenBuilder _builder = builder;
    private TimeSpan _expires;

    /// <summary>
    /// Gets the time at which the token was issued.
    /// </summary>
    public DateTime IssuedAt { get; } = issuedAt;
    /// <summary>
    /// Gets the expiration time of the token.
    /// </summary>
    public DateTime Expires { get; private set; } = expires;

    /// <summary>Get the JWT compact string.</summary>
    public string Token() => _token;


    /// <summary>
    /// Gets the <see cref="TokenValidationParameters"/> for validating the JWT token.
    /// </summary>
    /// <param name="clockSkew">Optional clock skew to allow when validating token lifetime.</param>
    /// <returns>The configured <see cref="TokenValidationParameters"/> instance.</returns>
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


    /// <summary>
    /// Re-issues a fresh token with the same claims, issuer, audience, and a new TTL synchronously.
    /// </summary>
    /// <param name="lifetime">Optional lifetime for the renewed token.</param>
    /// <returns>The renewed JWT compact string.</returns>
    public string Renew(TimeSpan? lifetime = null)
    {
        if (_key is null)
            throw new InvalidOperationException("Cannot renew: no symmetric key available.");
        return RenewAsync(_token, _key, lifetime).GetAwaiter().GetResult();
    }
    /// <summary>
    /// Asynchronously re-issues a fresh token with the same claims, issuer, audience, and a new TTL.
    /// </summary>
    /// <param name="lifetime">Optional lifetime for the renewed token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the renewed JWT compact string.</returns>
    public async Task<string> RenewAsync(TimeSpan? lifetime = null)
    {
        if (_key is null)
            throw new InvalidOperationException("Cannot renew: no symmetric key available.");
        return await RenewAsync(_token, _key, lifetime);
    }

    /// <summary>
    /// Re-issues a fresh token using the provided JWT, signing key, and optional lifetime.
    /// </summary>
    /// <param name="jwt">The JWT compact string to renew.</param>
    /// <param name="signingKey">The symmetric security key used for signing.</param>
    /// <param name="lifetime">Optional lifetime for the renewed token.</param>
    /// <returns>The renewed JWT compact string.</returns>
    /// <exception cref="SecurityTokenException"></exception>
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
        _expires = lifetime ?? (orig.ValidTo - orig.ValidFrom);

        return _builder.ValidFor(_expires).Build().Token();
    }
    /// <summary>
    /// Synchronously re-issues a fresh token using the provided JWT, signing key, and optional lifetime.
    /// </summary>
    /// <param name="jwt">The JWT compact string to renew.</param>
    /// <param name="signingKey">The symmetric security key used for signing.</param>
    /// <param name="lifetime">Optional lifetime for the renewed token.</param>
    /// <returns>The renewed JWT compact string.</returns>
    public string Renew(string jwt, SymmetricSecurityKey signingKey, TimeSpan? lifetime = null)
    {
        return RenewAsync(jwt, signingKey, lifetime).GetAwaiter().GetResult();
    }
}
