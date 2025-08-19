using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kestrun.Jwt;
using Xunit;

namespace Kestrun.Tests.Jwt;

public class JwtTokenBuilderTests
{
    [Theory]
    [InlineData(32, "HS256")] // 256-bit
    [InlineData(48, "HS384")] // 384-bit
    [InlineData(64, "HS512")] // 512-bit
    public void SignWithSecret_Auto_Picks_Expected_Alg(int bytes, string expectedAlg)
    {
        var secret = B64Url(Enumerable.Repeat((byte)0x01, bytes).ToArray());
        var b = JwtTokenBuilder.New().WithIssuer("iss").WithAudience("aud").WithSubject("s").SignWithSecret(secret);
        var res = b.Build();
        Assert.Equal(expectedAlg, b.Algorithm);
        Assert.Contains(expectedAlg, res.GetValidationParameters().ValidAlgorithms);
    }

    [Fact]
    public void AddHeader_Appears_In_Token_Header()
    {
        var secret = B64Url(Enumerable.Repeat((byte)0x02, 32).ToArray());
        var token = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("sub")
            .AddHeader("cty", "JWT")
            .SignWithSecret(secret)
            .Build()
            .Token();

        var p = JwtInspector.ReadAllParameters(token);
        Assert.Equal("JWT", p.Header["cty"]);
    }

    [Fact]
    public void EncryptWithSecret_dir_CBC_Builds_Token()
    {
        var sign = B64Url(Enumerable.Repeat((byte)0x03, 32).ToArray()); // HS256 signing
        var encKey = Enumerable.Repeat((byte)0x04, 32).ToArray();       // 256-bit key for A128CBC-HS256

        var token = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("sub")
            .SignWithSecret(sign)
            .EncryptWithSecret(encKey, keyAlg: "dir", encAlg: "A128CBC-HS256")
            .Build()
            .Token();

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void EncryptWithSecret_KeyWrapWrongSize_Throws()
    {
        var sign = B64Url(Enumerable.Repeat((byte)0x05, 32).ToArray());
        var encKey = Enumerable.Repeat((byte)0x06, 16).ToArray(); // 128-bit but A256GCM requires 256

        var b = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("sub")
            .SignWithSecret(sign)
            .EncryptWithSecret(encKey, keyAlg: "A256KW", encAlg: "A256GCM");

        Assert.Throws<ArgumentException>(() => b.Build());
    }

    [Fact]
    public void SignWithCertificate_ECDSA_ES256_Algorithm()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var cert = CreateEcdsaSelfSignedCertOrNull("CN=ES256", ecdsa);
        if (cert is null) { return; }
        // Skip if ES256 not actually usable for X509SecurityKey in this environment
        var cpf = new Microsoft.IdentityModel.Tokens.CryptoProviderFactory();
        var x509Key = new Microsoft.IdentityModel.Tokens.X509SecurityKey(cert);
        try { var sp = cpf.CreateForSigning(x509Key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.EcdsaSha256); sp.Dispose(); }
        catch (NotSupportedException) { return; }

        var b = JwtTokenBuilder.New().WithIssuer("iss").WithAudience("aud").WithSubject("s").SignWithCertificate(cert);
        var res = b.Build();
        Assert.Equal("ES256", b.Algorithm);
        Assert.Contains("ES256", res.GetValidationParameters().ValidAlgorithms);
        cert.Dispose();
    }

    private static string B64Url(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        s = s.Replace('+', '-').Replace('/', '_');
        return s.TrimEnd('=');
    }

    private static X509Certificate2? CreateEcdsaSelfSignedCertOrNull(string subject, ECDsa ecdsa)
    {
        // Use reflection to avoid compile-time dependency on CertificateRequest
        var typeName = "System.Security.Cryptography.X509Certificates.CertificateRequest, System.Security.Cryptography.X509Certificates";
        var t = Type.GetType(typeName, throwOnError: false);
        if (t == null)
        {
            return null;
        }

        var ctor = t.GetConstructors()
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 3 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(ECDsa) && ps[2].ParameterType == typeof(HashAlgorithmName);
            });
        if (ctor == null)
        {
            return null;
        }

        var req = ctor.Invoke(new object[] { subject, ecdsa, HashAlgorithmName.SHA256 });
        var mi = t.GetMethod("CreateSelfSigned", new[] { typeof(DateTimeOffset), typeof(DateTimeOffset) });
        if (mi == null)
        {
            return null;
        }
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = DateTimeOffset.UtcNow.AddMinutes(10);
        return (X509Certificate2?)mi.Invoke(req, new object[] { notBefore, notAfter });
    }
}
