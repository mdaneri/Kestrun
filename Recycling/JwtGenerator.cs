// File: JwtGenerator.cs
// NuGet: System.IdentityModel.Tokens.Jwt (>= 7.x)

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Kestrun.Security;

/// <summary>
///  One-stop helper for issuing JWS/JWE tokens.
///  – Pass <paramref name="signingCredentials"/> to sign (JWS)  
///  – Pass <paramref name="encryptingCredentials"/> to encrypt (JWE)  
///  – Pass *both* for nested “signed-then-encrypted” tokens.
/// </summary>
public static class JwtGenerator
{
    public static string GenerateJwt(
        IEnumerable<Claim>? claims = null,
        string? issuer = null,
        string? audience = null,
        DateTimeOffset? expires = null,
          // Optional parameters
          SigningCredentials? signingCredentials = null,
          DateTime? notBefore = null,
          EncryptingCredentials? encryptingCredentials = null,
          IDictionary<string, object>? additionalHeaders = null)
    {
        if (signingCredentials is null && encryptingCredentials is null)
            throw new ArgumentException(
                "You must supply signingCredentials, encryptingCredentials, or both.");

        var handler = new JwtSecurityTokenHandler();

        // `[]` requires C# 12; use Array.Empty<Claim>() for older language versions
        var identity = new ClaimsIdentity(claims ?? Array.Empty<Claim>());

        var effectiveNotBefore = notBefore ?? DateTime.UtcNow;

        var token = handler.CreateJwtSecurityToken(
            issuer, audience, identity,
            notBefore: effectiveNotBefore,
            expires: (expires ?? DateTimeOffset.UtcNow.AddHours(1)).UtcDateTime,
            issuedAt: DateTime.UtcNow,
            signingCredentials, encryptingCredentials);

        if (additionalHeaders != null)
            foreach (var kv in additionalHeaders)
                token.Header[kv.Key] = kv.Value;

        return handler.WriteToken(token);
    }
}
