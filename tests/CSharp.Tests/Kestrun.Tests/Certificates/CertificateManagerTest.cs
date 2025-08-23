using System.Security;
using Kestrun.Certificates;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Xunit;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KestrunTests.Certificates;

public class CertificateManagerTest
{
    private static CertificateManager.SelfSignedOptions DefaultSelfSignedOptions()
        => new(["localhost", "127.0.0.1"],
               KeyType: CertificateManager.KeyType.Rsa,
               KeyLength: 2048,
               ValidDays: 30,
               Ephemeral: true,
               Exportable: true);

    [Fact]
    public void NewSelfSigned_GeneratesValidCert_WithSAN_EKU()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());

        Assert.NotNull(cert);
        Assert.True(DateTime.UtcNow >= cert.NotBefore.ToUniversalTime().AddMinutes(-6));
        Assert.True(DateTime.UtcNow <= cert.NotAfter.ToUniversalTime().AddDays(1));
        Assert.True(cert.HasPrivateKey);
        Assert.Contains("CN=localhost", cert.Subject, StringComparison.OrdinalIgnoreCase);

        // SAN present
        Assert.Contains(
            cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>(),
            e => string.Equals(e.Oid?.Value, "2.5.29.17", StringComparison.Ordinal));

        // EKU contains serverAuth and clientAuth
        var eku = cert.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .SelectMany(e => e.EnhancedKeyUsages.Cast<Oid>())
            .Select(o => o.Value)
            .ToHashSet();
        Assert.Contains("1.3.6.1.5.5.7.3.1", eku); // serverAuth
        Assert.Contains("1.3.6.1.5.5.7.3.2", eku); // clientAuth
    }

    [Fact]
    public void NewCertificateRequest_ReturnsPemAndPrivateKey_WithSAN()
    {
        var csr = CertificateManager.NewCertificateRequest(
            new CertificateManager.CsrOptions([
                "localhost", "192.168.0.1"
            ], KeyType: CertificateManager.KeyType.Rsa, KeyLength: 2048, CommonName: "localhost"));

        Assert.NotNull(csr);
        Assert.NotNull(csr.PrivateKey);
        Assert.Contains("BEGIN CERTIFICATE REQUEST", csr.Pem);
        Assert.Contains("END CERTIFICATE REQUEST", csr.Pem);

        // Parse CSR with BouncyCastle to verify SAN
        using var sr = new StringReader(csr.Pem);
        var obj = new PemReader(sr).ReadObject();
        _ = Assert.IsType<Pkcs10CertificationRequest>(obj);
        var req = (Pkcs10CertificationRequest)obj;
        var attributes = req.GetCertificationRequestInfo().Attributes; // Asn1Set
        AttributePkcs? extAttr = null;
        for (var i = 0; i < (attributes?.Count ?? 0); i++)
        {
            var attr = AttributePkcs.GetInstance(attributes![i]);
            if (attr.AttrType.Equals(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest))
            {
                extAttr = attr;
                break;
            }
        }
        Assert.NotNull(extAttr);
        // Attribute value contains the extensions object directly; no need to force DerSet
        var extensions = X509Extensions.GetInstance(extAttr!.AttrValues[0]);
        var sanExt = extensions.GetExtension(X509Extensions.SubjectAlternativeName);
        Assert.NotNull(sanExt);
        var generalNames = GeneralNames.GetInstance(sanExt!.GetParsedValue()).GetNames();
        Assert.Contains(generalNames, n => n.TagNo == GeneralName.DnsName && string.Equals(n.Name.ToString(), "localhost", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generalNames, n => n.TagNo == GeneralName.IPAddress);
    }

    [Fact]
    public void ExportImport_Pfx_RoundTrip_WithPassword()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pfxPath = Path.Combine(dir, "testcert.pfx");
        var password = "s3cret".AsSpan();

        CertificateManager.Export(cert, pfxPath, CertificateManager.ExportFormat.Pfx, password);
        Assert.True(File.Exists(pfxPath));
        Assert.True(new FileInfo(pfxPath).Length > 0);

        var imported = CertificateManager.Import(pfxPath, password);
        Assert.NotNull(imported);
        Assert.True(imported.HasPrivateKey);
        Assert.Contains("CN=localhost", imported.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportImport_Pem_RoundTrip_WithEncryptedKey()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pemPath = Path.Combine(dir, "cert.pem");
        var pwd = "topsecret";

        var prevCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir; // key file is written to CWD by implementation
            // Use SecureString overload to avoid ambiguity and assert encrypted key is produced
            using var ss = new SecureString();
            foreach (var ch in pwd)
            {
                ss.AppendChar(ch);
            }
            ss.MakeReadOnly();
            CertificateManager.Export(cert, pemPath, CertificateManager.ExportFormat.Pem, ss, includePrivateKey: true);

            Assert.True(File.Exists(pemPath));
            var keyPath = Path.Combine(dir, "cert.key");
            Assert.True(File.Exists(keyPath));

            var keyText = File.ReadAllText(keyPath);
            Assert.Contains("ENCRYPTED PRIVATE KEY", keyText);

            // Import with encrypted key, retry up to 3 times if HasPrivateKey is false (to mitigate flakiness)
            X509Certificate2? imported = null;
            var hasPrivateKey = false;
            Exception? lastEx = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    imported = CertificateManager.Import(pemPath, pwd, keyPath);
                    hasPrivateKey = imported != null && imported.HasPrivateKey;
                    if (hasPrivateKey)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
                // Small delay to avoid timing issues
                Thread.Sleep(100);
            }
            Assert.NotNull(imported);
            Assert.True(hasPrivateKey, $"Imported certificate did not have a private key after 3 attempts. Last exception: {lastEx}\nKey file contents: {keyText}");
        }
        finally
        {
            Environment.CurrentDirectory = prevCwd;
        }
    }

    [Fact]
    public void ExportImport_Pem_PublicOnly()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pemPath = Path.Combine(dir, "pubonly.pem");

        CertificateManager.Export(cert, pemPath, CertificateManager.ExportFormat.Pem, [], includePrivateKey: false);
        Assert.True(File.Exists(pemPath));

        var imported = CertificateManager.Import(pemPath);
        Assert.NotNull(imported);
        Assert.False(imported.HasPrivateKey);
    }

    [Fact]
    public void Validate_SelfSigned_And_WeakRsa()
    {
        var good = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        Assert.True(CertificateManager.Validate(good));
        Assert.False(CertificateManager.Validate(good, denySelfSigned: true));

        var weak = CertificateManager.NewSelfSigned(new CertificateManager.SelfSignedOptions([
            "localhost"
        ], KeyType: CertificateManager.KeyType.Rsa, KeyLength: 1024, ValidDays: 7, Ephemeral: true, Exportable: true));

        Assert.False(CertificateManager.Validate(weak, allowWeakAlgorithms: false));
        Assert.True(CertificateManager.Validate(weak, allowWeakAlgorithms: true));
    }

    [Fact]
    public void GetPurposes_ReturnsAtLeastServerClientAuth()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        var purposes = CertificateManager.GetPurposes(cert).ToList();
        Assert.True(purposes.Count >= 2);
        // Accept either friendly names or OIDs
        Assert.Contains(purposes, p =>
            p.Contains("Server Authentication", StringComparison.OrdinalIgnoreCase)
            || p == "1.3.6.1.5.5.7.3.1");
        Assert.Contains(purposes, p =>
            p.Contains("Client Authentication", StringComparison.OrdinalIgnoreCase)
            || p == "1.3.6.1.5.5.7.3.2");
    }

    [Fact]
    public void Import_Throws_OnMissingFile() => _ = Assert.Throws<FileNotFoundException>(() => CertificateManager.Import(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cer")));

    [Fact]
    public void Import_Throws_OnEmptyPath() => _ = Assert.Throws<ArgumentException>(() => CertificateManager.Import(""));

    [Fact]
    public void Import_Pfx_With_SecureString_Password_Succeeds()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pfxPath = Path.Combine(dir, "secure.pfx");
        var pwd = "Sup3rS3cure".AsSpan();
        CertificateManager.Export(cert, pfxPath, CertificateManager.ExportFormat.Pfx, pwd);
        Assert.True(File.Exists(pfxPath));

        using var ss = new SecureString();
        foreach (var c in pwd)
        {
            ss.AppendChar(c);
        }
        ss.MakeReadOnly();

        var imported = CertificateManager.Import(pfxPath, ss);
        Assert.True(imported.HasPrivateKey);
        Assert.Contains("CN=localhost", imported.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_Pem_With_Unencrypted_SeparateKey_Manual()
    {
        // Generate a non-ephemeral exportable self-signed cert for key extraction
        var cert = CertificateManager.NewSelfSigned(new CertificateManager.SelfSignedOptions([
            "localhost", "127.0.0.1"
        ], KeyType: CertificateManager.KeyType.Rsa, KeyLength: 2048, ValidDays: 30, Ephemeral: false, Exportable: true));
        using var rsa = cert.GetRSAPrivateKey();
        Assert.NotNull(rsa);

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var certPemPath = Path.Combine(dir, "manual.pem");
        var keyPemPath = Path.Combine(dir, "manual.key");

        // Write certificate PEM
        var certDer = cert.Export(X509ContentType.Cert);
        var certB64 = Convert.ToBase64String(certDer, Base64FormattingOptions.InsertLineBreaks);
        File.WriteAllText(certPemPath, $"-----BEGIN CERTIFICATE-----\n{certB64}\n-----END CERTIFICATE-----\n");

        // Write unencrypted PKCS#1 RSA PRIVATE KEY (CreateFromPemFile supports it)
        var keyDer = rsa!.ExportRSAPrivateKey();
        var keyB64 = Convert.ToBase64String(keyDer, Base64FormattingOptions.InsertLineBreaks);
        File.WriteAllText(keyPemPath, $"-----BEGIN RSA PRIVATE KEY-----\n{keyB64}\n-----END RSA PRIVATE KEY-----\n");

        var imported = CertificateManager.Import(certPemPath, privateKeyPath: keyPemPath, flags: X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.Exportable);
        Assert.True(imported.HasPrivateKey);
        Assert.Contains("CN=localhost", imported.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Purposes_Strict_Mismatch_Fails_But_NonStrict_Subset_Passes()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        // Build OidCollections
        var serverAuth = new Oid("1.3.6.1.5.5.7.3.1");
        var clientAuth = new Oid("1.3.6.1.5.5.7.3.2");
        var subset = new OidCollection { serverAuth }; // only server
        var exact = new OidCollection { serverAuth, clientAuth };

        // Non-strict subset should succeed
        Assert.True(CertificateManager.Validate(cert, expectedPurpose: subset, strictPurpose: false));
        // Strict subset should fail (missing clientAuth)
        Assert.False(CertificateManager.Validate(cert, expectedPurpose: subset, strictPurpose: true));
        // Strict exact set should pass
        Assert.True(CertificateManager.Validate(cert, expectedPurpose: exact, strictPurpose: true));
    }

    [Fact]
    public void NewCertificateRequest_Ecdsa_Uses_ECDSA_KeyPair()
    {
        var csr = CertificateManager.NewCertificateRequest(new CertificateManager.CsrOptions([
            "localhost"
        ], KeyType: CertificateManager.KeyType.Ecdsa, KeyLength: 256, CommonName: "localhost"));
        Assert.NotNull(csr.PrivateKey);
        // Private key should be EC
        var ecKey = Assert.IsType<Org.BouncyCastle.Crypto.Parameters.ECPrivateKeyParameters>(csr.PrivateKey);
        Assert.NotNull(ecKey.Parameters);
    }
}
