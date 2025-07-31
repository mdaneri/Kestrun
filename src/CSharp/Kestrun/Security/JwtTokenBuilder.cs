// File: JwtTokenBuilder.cs
// Namespace: Kestrun.Security
// NuGet: <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.*" />

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using System.Buffers.Text;
using Serilog;
using Serilog.Events;
using Microsoft.IdentityModel.JsonWebTokens;  // For Base64UrlEncoder
namespace Kestrun.Security;

/// <summary>
/// Fluent utility to create any flavour of JWS/JWE in one line.
/// </summary>
/// <example>
/// // PowerShell usage:
/// $builder = [Kestrun.Security.JwtTokenBuilder]::New()
/// $token   = $builder
///             .WithSubject('admin')
///             .WithIssuer('https://issuer')
///             .WithAudience('api')
///             .SignWithSecret('uZ6zDP3CGK3rktmVOXQk8A')   # base64url
///             .EncryptWithCertificate($cert,'RSA-OAEP','A256GCM')
///             .Build()
/// Write-Output $token
/// </example>
public sealed class JwtTokenBuilder
{
    // ───── Public fluent API ──────────────────────────────────────────
    public static JwtTokenBuilder New() => new();

    public JwtTokenBuilder WithIssuer(string issuer) { _issuer = issuer; return this; }
    public JwtTokenBuilder WithAudience(string audience) { _aud = audience; return this; }
    public JwtTokenBuilder WithSubject(string sub) { _claims.Add(new Claim(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub, sub)); return this; }
    public JwtTokenBuilder AddClaim(string type, string value) { _claims.Add(new Claim(type, value)); return this; }
    public JwtTokenBuilder ValidFor(TimeSpan ttl) { _lifetime = ttl; return this; }
    public JwtTokenBuilder NotBefore(DateTime utc) { _nbf = utc; return this; }
    public JwtTokenBuilder AddHeader(string k, object v) { _header[k] = v; return this; }
    private SymmetricSecurityKey? _lastSigningKey;   // add near the other fields
                                                     // ── signing helpers

    public JwtTokenBuilder SignWithRsaPem(string pemPath, string alg = "auto")
    {
        _signCfg = new RsaSign(File.ReadAllText(pemPath), alg); return this;
    }
    public JwtTokenBuilder SignWithCertificate(X509Certificate2 cert, string alg = "auto")
    {
        _signCfg = new CertSign(cert, alg); return this;
    }

    public JwtTokenBuilder SignWithSecret(string b64Url, string alg = "auto")
    {
        byte[] raw = Base64UrlEncoder.DecodeBytes(b64Url);
        var key = new SymmetricSecurityKey(raw) { KeyId = Guid.NewGuid().ToString("N") };

        // pick algorithm when caller said "auto"
        string resolvedAlg = alg.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? raw.Length switch          // size in bytes → bits
            {
                >= 64 => SecurityAlgorithms.HmacSha512, // ≥ 512 bit
                >= 48 => SecurityAlgorithms.HmacSha384, // ≥ 384 bit
                _ => SecurityAlgorithms.HmacSha256  // everything else
            }
            : Map.Jws[alg];              // caller supplied HS256 / HS384 / …

        _lastSigningKey = key;
        _signCfg = new SymmetricSign(key, resolvedAlg);
        return this;
    }

    // 2️⃣ Hex secret just delegates
    public JwtTokenBuilder SignWithSecretHex(string hex, string alg = "auto")
    {
        return SignWithSecret(Base64UrlEncoder.Encode(Convert.FromHexString(hex)), alg);
    }

    // ── encryption helpers
    public JwtTokenBuilder EncryptWithCertificate(
        X509Certificate2 cert,
        string keyAlg = "RSA-OAEP",
        string encAlg = "A256GCM")
    {
        _encCfg = new CertEncrypt(cert, keyAlg, encAlg); return this;
    }
    public JwtTokenBuilder EncryptWithPemPublic(
        string pemPath,
        string keyAlg = "RSA-OAEP",
        string encAlg = "A256GCM")
    {
        _encCfg = new RsaEncrypt(File.ReadAllText(pemPath), keyAlg, encAlg); return this;
    }



    public JwtTokenBuilder EncryptWithSecretHex(string hex,
                                                string keyAlg = "dir",
                                                string encAlg = "A256CBC-HS512")
    {
        return EncryptWithSecret(Convert.FromHexString(hex), keyAlg, encAlg);
    }



    public JwtTokenBuilder EncryptWithSecretB64(
        string b64Url,
        string keyAlg = "dir",
        string encAlg = "A256CBC-HS512")
    {
        return EncryptWithSecret(Base64UrlEncoder.DecodeBytes(b64Url), keyAlg, encAlg);
    }

    public JwtTokenBuilder EncryptWithSecret(byte[] keyBytes,
                                         string keyAlg = "dir",
                                         string encAlg = "A256CBC-HS512")
    {
        string b64u = Base64UrlEncoder.Encode(keyBytes);
        _encCfg = new SymmetricEncrypt(b64u, keyAlg, encAlg);
        return this;
    }


    // ───── Build the compact JWT ──────────────────────────────────────
    public string Build()
    {
        if (_signCfg is null && _encCfg is null)
            throw new InvalidOperationException("Provide signing and/or encryption settings.");

        var handler = new JwtSecurityTokenHandler();

        var token = handler.CreateJwtSecurityToken(
            issuer: _issuer,
            audience: _aud,
            subject: new ClaimsIdentity(_claims),
            notBefore: _nbf,
            expires: _nbf.Add(_lifetime),
            issuedAt: DateTime.UtcNow,
            signingCredentials: _signCfg?.ToSigningCreds(),
            encryptingCredentials: _encCfg?.ToEncryptingCreds());

        foreach (var kv in _header) token.Header[kv.Key] = kv.Value;

        return handler.WriteToken(token);
    }

    public string Build(out SymmetricSecurityKey? signingKey)
    {
        string jwt = Build();          // call the original Build()
        signingKey = _lastSigningKey;  // may be null for unsigned / RSA / cert
        return jwt;
    }

    public static async Task<string> RenewAsync(string jwt, SymmetricSecurityKey signingKey, TimeSpan? lifetime = null)
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

        // ②  build new token with same claims / iss / aud
        var builder = JwtTokenBuilder.New()
                         .WithIssuer(orig.Issuer)
                         .WithAudience(orig.Audiences.FirstOrDefault() ?? "")
                         .ValidFor(lifetime ?? (orig.ValidTo - orig.ValidFrom));

        foreach (var c in orig.Claims)
            builder.AddClaim(c.Type, c.Value);

        // reuse symmetric key
        builder.SignWithSecret(
            Base64UrlEncoder.Encode(signingKey.Key), "HS256");

        return builder.Build();
    }
    public static string Renew(string jwt, SymmetricSecurityKey signingKey, TimeSpan? lifetime = null)
    {
        return RenewAsync(jwt, signingKey, lifetime).GetAwaiter().GetResult();
    }

    // ───── Internals ──────────────────────────────────────────────────
    private readonly List<Claim> _claims = new();
    private readonly Dictionary<string, object> _header = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _nbf = DateTime.UtcNow;
    private TimeSpan _lifetime = TimeSpan.FromHours(1);
    private string? _issuer, _aud;
    private ISignConfig? _signCfg;
    private IEncConfig? _encCfg;

    // ── Strategy interfaces & concrete configs ───────────────────────
    private interface ISignConfig { SigningCredentials ToSigningCreds(); }
    private interface IEncConfig { EncryptingCredentials ToEncryptingCreds(); }

    private static class Map
    {
        // maps short names (HS256, RSA-OAEP, …) to SecurityAlgorithms
        public static readonly IReadOnlyDictionary<string, string> Jws = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HS256"] = SecurityAlgorithms.HmacSha256,
            ["HS384"] = SecurityAlgorithms.HmacSha384,
            ["HS512"] = SecurityAlgorithms.HmacSha512,
            ["RS256"] = SecurityAlgorithms.RsaSha256,
            ["RS384"] = SecurityAlgorithms.RsaSha384,
            ["RS512"] = SecurityAlgorithms.RsaSha512,
            ["PS256"] = SecurityAlgorithms.RsaSsaPssSha256,
            ["PS384"] = SecurityAlgorithms.RsaSsaPssSha384,
            ["PS512"] = SecurityAlgorithms.RsaSsaPssSha512,
            ["ES256"] = SecurityAlgorithms.EcdsaSha256,
            ["ES384"] = SecurityAlgorithms.EcdsaSha384,
            ["ES512"] = SecurityAlgorithms.EcdsaSha512
        };
        public static readonly IReadOnlyDictionary<string, string> KeyAlg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RSA-OAEP"] = SecurityAlgorithms.RsaOAEP,
            ["RSA-OAEP-256"] = "RSA-OAEP-256",
            ["RSA-OAEP-384"] = "RSA-OAEP-384",
            ["RSA-OAEP-512"] = "RSA-OAEP-512",
            ["RSA1_5"] = SecurityAlgorithms.RsaPKCS1,
            ["A128KW"] = SecurityAlgorithms.Aes128KW,
            ["A192KW"] = SecurityAlgorithms.Aes192KW,
            ["A256KW"] = SecurityAlgorithms.Aes256KW,
            ["ECDH-ES"] = SecurityAlgorithms.EcdhEs,
            ["ECDH-ES+A128KW"] = SecurityAlgorithms.EcdhEsA128kw,
            ["ECDH-ES+A192KW"] = SecurityAlgorithms.EcdhEsA192kw,
            ["ECDH-ES+A256KW"] = SecurityAlgorithms.EcdhEsA256kw,
            ["dir"] = "dir"
        };
        public static readonly IReadOnlyDictionary<string, string> EncAlg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A128GCM"] = SecurityAlgorithms.Aes128Gcm,
            ["A192GCM"] = SecurityAlgorithms.Aes192Gcm,
            ["A256GCM"] = SecurityAlgorithms.Aes256Gcm,
            ["A128CBC-HS256"] = SecurityAlgorithms.Aes128CbcHmacSha256,
            ["A192CBC-HS384"] = SecurityAlgorithms.Aes192CbcHmacSha384,
            ["A256CBC-HS512"] = SecurityAlgorithms.Aes256CbcHmacSha512
        };
    }

    // ── Signing configs ───────────────────────────────────────────────
    private sealed record SymmetricSign(
         SecurityKey Key, string ResolvedAlg) : ISignConfig
    {
        public SigningCredentials ToSigningCreds()
            => new(Key, ResolvedAlg);
    }

    private sealed record RsaSign(string Pem, string Alg) : ISignConfig
    {
        public SigningCredentials ToSigningCreds()
        {
            var rsa = RSA.Create(); rsa.ImportFromPem(Pem);
            var key = new RsaSecurityKey(rsa);
            var algo = Alg.Equals("auto", StringComparison.OrdinalIgnoreCase) ? Map.Jws["RS256"] : Map.Jws[Alg];
            return new SigningCredentials(key, algo);
        }
    }


    private sealed record CertSign(X509Certificate2 Cert, string Alg) : ISignConfig
    {
        public SigningCredentials ToSigningCreds()
        {
            if (!Cert.HasPrivateKey)
                throw new ArgumentException("Certificate must contain a private key.");

            var key = new X509SecurityKey(Cert);

            // Pick default alg if caller passed "auto"
            string resolvedAlg;
            if (!Alg.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                resolvedAlg = Map.Jws[Alg];
            }
            else
            {
                if (Cert.GetECDsaPublicKey() is not null)
                    resolvedAlg = Map.Jws["ES256"];   // ECDSA → ES256 by default
                else if (Cert.GetRSAPublicKey() is not null)
                    resolvedAlg = Map.Jws["RS256"];   // RSA   → RS256 by default
                else
                {
                    string keyType = "unknown";
                    if (Cert.PublicKey != null && Cert.PublicKey.EncodedKeyValue != null && Cert.PublicKey.EncodedKeyValue.Oid != null)
                        keyType = Cert.PublicKey.EncodedKeyValue.Oid.FriendlyName ?? "unknown";
                    throw new NotSupportedException(
                        $"Unsupported key type: {keyType}");
                }
            }

            return new SigningCredentials(key, resolvedAlg);
        }
    }

    // ── Encryption configs ────────────────────────────────────────────
    private abstract record BaseEnc(string KeyAlg, string EncAlg) : IEncConfig
    {
        protected string KeyAlgMapped => Map.KeyAlg[KeyAlg];
        protected string EncAlgMapped => Map.EncAlg[EncAlg];
        public abstract EncryptingCredentials ToEncryptingCreds();
    }

    private sealed record CertEncrypt(X509Certificate2 Cert, string KeyAlg, string EncAlg) : BaseEnc(KeyAlg, EncAlg)
    {
        public override EncryptingCredentials ToEncryptingCreds()
        {
            var key = new X509SecurityKey(Cert);
            return new EncryptingCredentials(key, KeyAlgMapped, EncAlgMapped);
        }
    }

    private sealed record RsaEncrypt(string Pem, string KeyAlg, string EncAlg) : BaseEnc(KeyAlg, EncAlg)
    {
        public override EncryptingCredentials ToEncryptingCreds()
        {
            var rsa = RSA.Create(); rsa.ImportFromPem(Pem);
            var key = new RsaSecurityKey(rsa);
            return new EncryptingCredentials(key, KeyAlgMapped, EncAlgMapped);
        }
    }

    private sealed record SymmetricEncrypt(
     string B64,
     string KeyAlg,
     string EncAlg) : BaseEnc(KeyAlg, EncAlg)
    {
        public override EncryptingCredentials ToEncryptingCreds()
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Encrypting with {KeyAlg} and {EncAlg} ({Bits} bits)",
                    KeyAlg, EncAlg, Base64UrlEncoder.DecodeBytes(B64).Length * 8);
            }
            // ────────── shared-secret → SymmetricSecurityKey ──────────
            if (!Map.KeyAlg.ContainsKey(KeyAlg))
                throw new ArgumentException($"Unknown key algorithm: {KeyAlg}");

            var key = new SymmetricSecurityKey(Base64UrlEncoder.DecodeBytes(B64));
            int bits = key.KeySize;                        // 128 / 192 / 256 / 384 / 512 …

            // ────────── auto-pick encAlg for 'dir' default case ───────
            string encEff = EncAlg;

            if (KeyAlg.Equals("dir", StringComparison.OrdinalIgnoreCase) &&
                EncAlg.Equals("A256CBC-HS512", StringComparison.OrdinalIgnoreCase))
            {
                encEff = bits switch
                {
                    128 => "A128GCM",
                    192 => "A192GCM",
                    256 => "A256GCM",
                    384 => "A192CBC-HS384",
                    512 => "A256CBC-HS512",
                    _ => throw new ArgumentException(
                               $"Unsupported key size {bits} bits for direct encryption.")
                };
            }

            // ────────── hard validation (caller may specify any enc) ──
            static void Require(int actualBits, int requiredBits, string alg) =>
                _ = actualBits == requiredBits
                    ? true
                    : throw new ArgumentException($"{alg} requires a {requiredBits}-bit key.");

            switch (encEff.ToUpperInvariant())
            {
                case "A128GCM": Require(bits, 128, encEff); break;
                case "A192GCM": Require(bits, 192, encEff); break;
                case "A256GCM": Require(bits, 256, encEff); break;
                case "A128CBC-HS256": Require(bits, 256, encEff); break;
                case "A192CBC-HS384": Require(bits, 384, encEff); break;
                case "A256CBC-HS512": Require(bits, 512, encEff); break;
                default:
                    throw new ArgumentException($"Unknown or unsupported enc algorithm: {encEff}");
            }
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Encrypting with {KeyAlg} and {EncAlg} ({Bits} bits)",
                    KeyAlg, encEff, bits);
            }
            // ────────── build EncryptingCredentials ───────────────────
            return new EncryptingCredentials(
                key,
                Map.KeyAlg[KeyAlg.ToUpper()],          // 'dir', 'A256KW', …
                Map.EncAlg[encEff.ToUpper()]);         // validated / auto-picked enc
        }
    }

    public JwtTokenPackage BuildPackage()
{
    string jwt = Build(out var key);   // your existing overload

    var tvp = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidIssuer              = _issuer,      // private fields in builder
        ValidateAudience         = true,
        ValidAudience            = _aud,
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.FromMinutes(1),

        RequireSignedTokens      = key is not null,
        ValidateIssuerSigningKey = key is not null,
        IssuerSigningKey         = key,
        ValidAlgorithms          = key is not null
            ? new[] { SecurityAlgorithms.HmacSha256 }
            : Array.Empty<string>()
    };

    return new JwtTokenPackage(jwt, key, tvp);
}


}