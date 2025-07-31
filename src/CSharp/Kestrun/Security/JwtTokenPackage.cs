using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Kestrun.Security;

public sealed record JwtTokenPackage(
    string Token,
    SymmetricSecurityKey? SigningKey,
    TokenValidationParameters ValidationParameters)
{
    /// Validate another JWT against this package’s rules & key.
    public bool Validate(string jwt, out ClaimsPrincipal principal)
    {
        var handler = new JsonWebTokenHandler();

        // new async API, blocked for convenience
        var result = handler.ValidateTokenAsync(jwt, ValidationParameters)
                            .GetAwaiter()
                            .GetResult();

        principal = result.ClaimsIdentity is null
                   ? new ClaimsPrincipal()
                   : new ClaimsPrincipal(result.ClaimsIdentity);
        return result.IsValid;
    }


    /// Issue a new token with same claims/iss/aud and same TTL.
    public string Renew(TimeSpan? newTtl = null)
    {
        if (SigningKey is null)
            throw new InvalidOperationException("Cannot renew: package has no symmetric signing key.");

        var handler = new JsonWebTokenHandler();
        var orig = (JsonWebToken)handler.ReadToken(Token);

        // build new token
        var builder = JwtTokenBuilder.New()
                         .WithIssuer(orig.Issuer)
                         .WithAudience(orig.Audiences.FirstOrDefault() ?? "")
                         .ValidFor(newTtl ?? (orig.ValidTo - orig.ValidFrom));

        foreach (var c in orig.Claims)
            builder.AddClaim(c.Type, c.Value);

        builder.SignWithSecret(
            Base64UrlEncoder.Encode(SigningKey.Key),   // reuse same secret
            "HS256");

        return builder.Build();
    }
}
