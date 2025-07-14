using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using System.Runtime.InteropServices;
using System.Text;
using Org.BouncyCastle.Asn1.X9;


namespace KestrunLib;

/// <summary>
/// Drop-in replacement for Pode’s certificate helpers, powered by Bouncy Castle.
/// </summary>
public static class CertificateManager
{
    #region  enums / option records
    public enum KeyType { Rsa, Ecdsa }
    public enum ExportFormat { Pfx, Pem }

    public record SelfSignedOptions(
        IEnumerable<string> DnsNames,
        KeyType KeyType = KeyType.Rsa,
        int KeyLength = 2048,
        IEnumerable<KeyPurposeID>? Purposes = null,
        int ValidDays = 365,
        bool Ephemeral = false,
        bool Exportable = false);

    public record CsrOptions(
        IEnumerable<string> DnsNames,
        KeyType KeyType = KeyType.Rsa,
        int KeyLength = 2048,
        string? Country = null,
        string? Org = null,
        string? OrgUnit = null,
        string? CommonName = null);
    #endregion

    #region  Self-signed certificate
    public static X509Certificate2 NewSelfSigned(SelfSignedOptions o)
    {
        var random = new SecureRandom(new CryptoApiRandomGenerator());

        // ── 1. Key pair ───────────────────────────────────────────────────────────
        AsymmetricCipherKeyPair keyPair =
            o.KeyType switch
            {
                KeyType.Rsa => GenRsaKeyPair(o.KeyLength, random),
                KeyType.Ecdsa => GenEcKeyPair(o.KeyLength, random),
                _ => throw new ArgumentOutOfRangeException()
            };

        // ── 2. Certificate body ───────────────────────────────────────────────────
        var notBefore = DateTime.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddDays(o.ValidDays);
        var serial = BigIntegers.CreateRandomInRange(
                            BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), random);

        var subjectDn = new X509Name($"CN={o.DnsNames.First()}");
        var gen = new X509V3CertificateGenerator();
        gen.SetSerialNumber(serial);
        gen.SetIssuerDN(subjectDn);
        gen.SetSubjectDN(subjectDn);
        gen.SetNotBefore(notBefore);
        gen.SetNotAfter(notAfter);
        gen.SetPublicKey(keyPair.Public);

        // SANs
        var altNames = o.DnsNames
                        .Select(n => new GeneralName(
                            IPAddress.TryParse(n, out _) ?
                                GeneralName.IPAddress : GeneralName.DnsName, n))
                        .ToArray();
        gen.AddExtension(X509Extensions.SubjectAlternativeName, false,
                         new DerSequence(altNames));

        // EKU
        var eku = o.Purposes ??
         [
             KeyPurposeID.id_kp_serverAuth,
            KeyPurposeID.id_kp_clientAuth
         ];
        gen.AddExtension(X509Extensions.ExtendedKeyUsage, false,
                         new ExtendedKeyUsage([.. eku]));

        // KeyUsage – allow digitalSignature & keyEncipherment
        gen.AddExtension(X509Extensions.KeyUsage, true,
                         new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

        // ── 3. Sign & output ──────────────────────────────────────────────────────
        var sigAlg = o.KeyType == KeyType.Rsa ? "SHA256WITHRSA" : "SHA384WITHECDSA";
        var signer = new Asn1SignatureFactory(sigAlg, keyPair.Private, random);
        var cert = gen.Generate(signer);

        return ToX509Cert2(cert, keyPair.Private,
            o.Exportable ? X509KeyStorageFlags.Exportable : X509KeyStorageFlags.DefaultKeySet,
            o.Ephemeral);
    }
    #endregion

    #region  CSR



    public static (string csrPem, AsymmetricKeyParameter privateKey)
     NewCertificateRequest(CsrOptions o)
    {
        // 0️⃣ Setup
        var random = new SecureRandom(new CryptoApiRandomGenerator());
        var keyPair = o.KeyType switch
        {
            KeyType.Rsa => GenRsaKeyPair(o.KeyLength, random),
            KeyType.Ecdsa => GenEcKeyPair(o.KeyLength, random),
            _ => throw new ArgumentOutOfRangeException()
        };

        // 1️⃣ Subject DN
        var order = new List<DerObjectIdentifier>();
        var attrs = new Dictionary<DerObjectIdentifier, string>();
        void Add(DerObjectIdentifier oid, string? v)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                order.Add(oid);
                attrs[oid] = v!;
            }
        }
        Add(X509Name.C, o.Country);
        Add(X509Name.O, o.Org);
        Add(X509Name.OU, o.OrgUnit);
        Add(X509Name.CN, o.CommonName ?? o.DnsNames.First());
        var subject = new X509Name(order, attrs);

        // 2️⃣ Build the SAN extension
        var altNames = o.DnsNames
            .Select(d => new GeneralName(
                IPAddress.TryParse(d, out _)
                    ? GeneralName.IPAddress
                    : GeneralName.DnsName, d))
            .ToArray();
        var sanSeq = new DerSequence(altNames);

        var extGen = new X509ExtensionsGenerator();
        extGen.AddExtension(
            X509Extensions.SubjectAlternativeName,
            critical: false,
            sanSeq);
        // Generate the X509Extensions object
        var extensions = extGen.Generate();

        // Wrap it in the pkcs#9 extensionRequest attribute
        var extensionRequestAttr = new AttributePkcs(
            PkcsObjectIdentifiers.Pkcs9AtExtensionRequest,
            new DerSet(extensions));
        var attrSet = new DerSet(extensionRequestAttr);

        // 3️⃣ Build and sign the CSR
        var sigAlg = o.KeyType == KeyType.Rsa
            ? "SHA256WITHRSA"
            : "SHA384WITHECDSA";

        var csr = new Pkcs10CertificationRequest(
            sigAlg,
            subject,
            keyPair.Public,
            attrSet,
            keyPair.Private);

        // 4️⃣ Output PEM
        using var sw = new StringWriter();
        new PemWriter(sw).WriteObject(csr);

        return (sw.ToString(), keyPair.Private);
    }

    #endregion

    #region  Import / Export
    public static X509Certificate2 Import(string path, string? password = null)
    {
        var bytes = File.ReadAllBytes(path);
        return new X509Certificate2(bytes, password,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
    }

    public static void Export(
    X509Certificate2 cert,
    string filePath,
    ExportFormat fmt,
    ReadOnlySpan<char> password = default,
    bool includePrivateKey = false)
    {
        // build the two password shapes we need once
        string? pwdString = password.IsEmpty ? null : new string(password); // for cert.Export
        char[]? pwdChars = password.IsEmpty ? null : password.ToArray();   // for Pkcs12Store.Load

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        switch (fmt)
        {
            case ExportFormat.Pfx:
                {
                    var pfx = cert.Export(X509ContentType.Pfx, pwdString);
                    File.WriteAllBytes($"{filePath}.pfx", pfx);
                    break;
                }

            case ExportFormat.Pem:
                {
                    using (var sw = new StreamWriter(path: $"{filePath}.crt", append: false, encoding: Encoding.ASCII))
                        new PemWriter(sw).WriteObject(
                            Org.BouncyCastle.Security.DotNetUtilities.FromX509Certificate(cert));

                    if (includePrivateKey)
                    {
                        if (pwdChars is null)
                            throw new ArgumentException(
                                "Password is required when exporting the private key.", nameof(password));

                        var pfx = cert.Export(X509ContentType.Pkcs12, pwdString);
                        var store = new Pkcs12StoreBuilder().Build();
                        store.Load(new MemoryStream(pfx), pwdChars);

                        var alias = store.Aliases.Cast<string>().Single(store.IsKeyEntry);
                        var key = store.GetKey(alias).Key;

                        using var pk = new StreamWriter(path: $"{filePath}.key", append: false, encoding: Encoding.ASCII);
                        new PemWriter(pk).WriteObject(key);
                    }
                    break;
                }
        }

        // scrub the char[] copy; string cannot be cleared
        if (pwdChars is not null)
            Array.Clear(pwdChars, 0, pwdChars.Length);
    }



    #endregion

    #region  Validation helpers (Test-PodeCertificate equivalent)
    public static bool Validate(
     X509Certificate2 cert,
     bool checkRevocation = false,
     bool allowWeakAlgorithms = false,
     bool denySelfSigned = false,
     OidCollection? expectedPurpose = null,
     bool strictPurpose = false)
    {
        // ── 1. Validity period ────────────────────────────────────────
        if (DateTime.UtcNow < cert.NotBefore || DateTime.UtcNow > cert.NotAfter)
            return false;

        // ── 2. Self-signed check ──────────────────────────────────────
        if (denySelfSigned && cert.Subject == cert.Issuer)
            return false;

        // ── 3. Chain building  + optional revocation ──────────────────
        using (var chain = new X509Chain())
        {
            chain.ChainPolicy.RevocationMode =
                checkRevocation ? X509RevocationMode.Online
                                : X509RevocationMode.NoCheck;

            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EndCertificateOnly;

            if (!chain.Build(cert))
                return false;
        }

        // ── 4. Enhanced Key Usage (EKU) validation ────────────────────
        if (expectedPurpose is { Count: > 0 })
        {
            var eku = cert.Extensions
                             .OfType<X509EnhancedKeyUsageExtension>()
                             .SelectMany(e => e.EnhancedKeyUsages.Cast<Oid>())
                             .Select(o => o.Value)
                             .ToHashSet();

            var wanted = expectedPurpose.Cast<Oid>()
                                        .Select(o => o.Value)
                                        .ToHashSet();

            bool ok = strictPurpose ? eku.SetEquals(wanted)
                                    : wanted.All(eku.Contains);

            if (!ok) return false;
        }

        // ── 5. Weak-algorithm rules (SHA-1, small keys) ───────────────
        if (!allowWeakAlgorithms)
        {
            bool isSha1 = cert.SignatureAlgorithm?.FriendlyName?
                               .Contains("sha1", StringComparison.OrdinalIgnoreCase) == true;

            bool weakRsa = cert.GetRSAPublicKey() is { KeySize: < 2048 };
            bool weakDsa = cert.GetDSAPublicKey() is { KeySize: < 2048 };
            bool weakEcdsa = cert.GetECDsaPublicKey() is { KeySize: < 256 };  // P-256 minimum

            if (isSha1 || weakRsa || weakDsa || weakEcdsa)
                return false;
        }

        return true;   // ✅ everything passed
    }


    public static IEnumerable<string> GetPurposes(X509Certificate2 cert) =>
    cert.Extensions
        .OfType<X509EnhancedKeyUsageExtension>()
        .SelectMany(x => x.EnhancedKeyUsages.Cast<Oid>())
        .Select(o => (o.FriendlyName ?? o.Value)!)   // ← null-forgiving
        .Where(s => s.Length > 0);                   // optional: drop empties
    #endregion

    #region  private helpers
    private static AsymmetricCipherKeyPair GenRsaKeyPair(int bits, SecureRandom rng)
    {
        var gen = new RsaKeyPairGenerator();
        gen.Init(new KeyGenerationParameters(rng, bits));
        return gen.GenerateKeyPair();
    }

    private static AsymmetricCipherKeyPair GenEcKeyPair(int bits, SecureRandom rng)
    {
        // NIST-style names are fine here
        var name = bits switch
        {
            <= 256 => "P-256",
            <= 384 => "P-384",
            _ => "P-521"
        };

        // ECNamedCurveTable knows about SEC *and* NIST names   
        var ecParams = ECNamedCurveTable.GetByName(name)
                       ?? throw new InvalidOperationException($"Curve not found: {name}");

        var domain = new ECDomainParameters(
            ecParams.Curve, ecParams.G, ecParams.N, ecParams.H, ecParams.GetSeed());

        var gen = new ECKeyPairGenerator();
        gen.Init(new ECKeyGenerationParameters(domain, rng));
        return gen.GenerateKeyPair();
    }

    private static X509Certificate2 ToX509Cert2(
        Org.BouncyCastle.X509.X509Certificate cert,
        AsymmetricKeyParameter privKey,
        X509KeyStorageFlags flags,
        bool ephemeral)
    {
        var store = new Pkcs12StoreBuilder().Build();
        var entry = new X509CertificateEntry(cert);
        const string alias = "cert";
        store.SetCertificateEntry(alias, entry);
        store.SetKeyEntry(alias, new AsymmetricKeyEntry(privKey),
                          [entry]);

        using var ms = new MemoryStream();
        store.Save(ms, [], new SecureRandom());
        return new X509Certificate2(ms.ToArray(), (string?)null,
            flags | (ephemeral ? X509KeyStorageFlags.EphemeralKeySet : 0));
    }
    #endregion
}
