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
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Events;


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
    /// <summary>
    /// Generates a new self-signed certificate using BouncyCastle.
    /// </summary>
    /// <param name="o">Options controlling key type, validity and extensions.</param>
    /// <returns>The generated certificate with private key.</returns>
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



    /// <summary>
    /// Creates a certificate signing request (CSR) and returns its PEM along with the generated private key.
    /// </summary>
    /// <param name="o">Options describing the subject and key parameters.</param>
    /// <returns>The CSR PEM string and the private key.</returns>
    public static (string csrPem, AsymmetricKeyParameter privateKey) NewCertificateRequest(CsrOptions o)
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

    #region  Import
    /// <summary>
    /// Imports a certificate from disk in various formats.
    /// </summary>
    /// <param name="certPath">Path to the certificate file.</param>
    /// <param name="password">Password for encrypted PFX or PEM keys.</param>
    /// <param name="privateKeyPath">Optional separate private key file.</param>
    /// <param name="flags">Key storage flags.</param>
    /// <returns>The loaded certificate.</returns>
    public static X509Certificate2 Import(
       string certPath,
       ReadOnlySpan<char> password = default,
       string? privateKeyPath = null,
       X509KeyStorageFlags flags = X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.Exportable)
    {
        if (string.IsNullOrEmpty(certPath))
            throw new ArgumentException("Certificate path cannot be null or empty.", nameof(certPath));
        if (!File.Exists(certPath))
            throw new FileNotFoundException("Certificate file not found.", certPath);
        if (!string.IsNullOrEmpty(privateKeyPath) && !File.Exists(privateKeyPath))
            throw new FileNotFoundException("Private key file not found.", privateKeyPath);
        var ext = Path.GetExtension(certPath).ToLowerInvariant();

        switch (ext)
        {
            // — PFX/PKCS#12 with embedded key — 
            case ".pfx":
            case ".p12":
                // Uses the X509Certificate2(byte[], ReadOnlySpan<char>, flags) ctor
                return new X509Certificate2(
                    File.ReadAllBytes(certPath),
                    password,
                    flags
                );

            // — DER-encoded public cert — 
            case ".cer":
            case ".der":
                return new X509Certificate2(File.ReadAllBytes(certPath));

            // — PEM (.pem/.crt): cert alone or cert+key, encrypted or not — 
            case ".pem":
            case ".crt":
                if (string.IsNullOrEmpty(privateKeyPath))
                {
                    if (password.IsEmpty)
                    {
                        return LoadCertOnlyPem(certPath);
                    }
                    else
                    {
                        // encrypted key is in the same file
                        return X509Certificate2.CreateFromEncryptedPemFile(certPath, password);
                    }
                }
                else if (!password.IsEmpty)
                {
                    // Encrypted private key in the PEM (same file or separate)
                    return X509Certificate2.CreateFromEncryptedPemFile(
                        certPath,
                        password,
                        privateKeyPath
                    );  // :contentReference[oaicite:0]{index=0}
                }
                else
                {
                    // Unencrypted: cert alone, or cert + unencrypted key (same file or separate)
                    return X509Certificate2.CreateFromPemFile(
                        certPath,
                        privateKeyPath
                    );  // :contentReference[oaicite:1]{index=1}
                }

            default:
                throw new NotSupportedException(
                    $"Certificate extension '{ext}' is not supported."
                );
        }
    }

    /// <summary>
    /// Loads a certificate from a PEM file that contains *only* a CERTIFICATE block (no key).
    /// </summary>
    private static X509Certificate2 LoadCertOnlyPem(string certPath)
    {
        // 1) Read + trim the whole PEM text
        string pem = File.ReadAllText(certPath).Trim();

        // 2) Define the BEGIN/END markers
        const string begin = "-----BEGIN CERTIFICATE-----";
        const string end = "-----END CERTIFICATE-----";

        // 3) Find their positions
        int start = pem.IndexOf(begin, StringComparison.Ordinal);
        if (start < 0) throw new InvalidDataException("BEGIN CERTIFICATE marker not found");
        start += begin.Length;

        int stop = pem.IndexOf(end, start, StringComparison.Ordinal);
        if (stop < 0) throw new InvalidDataException("END CERTIFICATE marker not found");

        // 4) Extract, clean, and decode the Base64 payload
        string b64 = pem[start..stop]
                       .Replace("\r", "")
                       .Replace("\n", "")
                       .Trim();
        byte[] der = Convert.FromBase64String(b64);

        // 5) Return the X509Certificate2
        return new X509Certificate2(der);
    }

    /// <summary>
    /// Imports a certificate using a <see cref="SecureString"/> password.
    /// </summary>
    /// <param name="certPath">Path to the certificate file.</param>
    /// <param name="password">Password stored as <see cref="SecureString"/>.</param>
    /// <param name="privateKeyPath">Optional private key path.</param>
    /// <param name="flags">Key storage flags.</param>
    /// <returns>The loaded certificate.</returns>
    public static X509Certificate2 Import(
       string certPath,
       SecureString password,
       string? privateKeyPath = null,
       X509KeyStorageFlags flags = X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.Exportable)
    {
        X509Certificate2? result = null;
        Log.Debug("Importing certificate from {CertPath} with flags {Flags}", certPath, flags);
        // ToSecureSpan zero-frees its buffer as soon as this callback returns.
        password.ToSecureSpan(span =>
        {
            // capture the return value of the span-based overload
            result = Import(certPath: certPath, password: span, privateKeyPath: privateKeyPath, flags: flags);
        });

        // at this point, unmanaged memory is already zeroed
        return result!;   // non-null because the callback always runs exactly once
    }

    /// <summary>
    /// Imports a certificate that does not require a password.
    /// </summary>
    /// <param name="certPath">Path to the certificate file.</param>
    /// <param name="privateKeyPath">Optional separate key file.</param>
    /// <param name="flags">Key storage flags.</param>
    /// <returns>The loaded certificate.</returns>
    public static X509Certificate2 Import(
         string certPath,
         string? privateKeyPath = null,
         X509KeyStorageFlags flags = X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.Exportable)
    {
        X509Certificate2? result = null;

        // ToSecureSpan zero-frees its buffer as soon as this callback returns.
        ReadOnlySpan<char> passwordSpan = default;
        // capture the return value of the span-based overload
        result = Import(certPath: certPath, password: passwordSpan, privateKeyPath: privateKeyPath, flags: flags);
        return result!;
    }

    /// <summary>
    /// Convenience overload for importing an unencrypted certificate with default flags.
    /// </summary>
    /// <param name="certPath">Path to the certificate file.</param>
    /// <returns>The loaded certificate.</returns>
    public static X509Certificate2 Import(string certPath)
    {
        X509Certificate2? result = null;

        // ToSecureSpan zero-frees its buffer as soon as this callback returns.
        ReadOnlySpan<char> passwordSpan = default;
        // capture the return value of the span-based overload
        result = Import(certPath: certPath, password: passwordSpan, privateKeyPath: null);
        return result!;
    }



    #endregion

    #region Export
    /// <summary>
    /// Exports a certificate to PFX or PEM format.
    /// </summary>
    /// <param name="cert">Certificate to export.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="fmt">Export format.</param>
    /// <param name="password">Password used for PFX or encrypted key.</param>
    /// <param name="includePrivateKey">Whether to include the private key in PEM exports.</param>
    public static void Export(X509Certificate2 cert, string filePath, ExportFormat fmt,
           ReadOnlySpan<char> password = default, bool includePrivateKey = false)
    {
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
        switch (fileExtension)
        {
            case ".pfx":
                if (fmt != ExportFormat.Pfx)
                    throw new NotSupportedException(
                        $"File extension '{fileExtension}' for '{filePath}' is not supported for PFX certificates.");
                break;


            case ".pem":
                if (fmt != ExportFormat.Pem)
                    throw new NotSupportedException(
                        $"File extension '{fileExtension}' for '{filePath}' is not supported for PEM certificates.");
                break;

            case "":
                // no extension, use the format as the extension
                filePath += fmt == ExportFormat.Pfx ? ".pfx" : ".pem";
                break;
            default:
                throw new NotSupportedException(
                    $"File extension '{fileExtension}' for '{filePath}' is not supported. Use .pfx or .pem.");
        }

        // ensure directory exists
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            throw new DirectoryNotFoundException(
                $"Directory '{dir}' does not exist. Cannot export certificate to {filePath}.");

        // build both shapes once
        using SecureString? pwdString = password.IsEmpty
            ? null
            : ToSecureString(password);
        char[]? pwdChars = password.IsEmpty
            ? null
            : password.ToArray();

        try
        {
            switch (fmt)
            {
                case ExportFormat.Pfx:
                    {
                        byte[] pfx = cert.Export(X509ContentType.Pfx, pwdString);
                        File.WriteAllBytes(filePath, pfx);
                        break;
                    }
                case ExportFormat.Pem:
                    {
                        // export cert
                        using var sw = new StreamWriter(filePath, false, Encoding.ASCII);
                        new PemWriter(sw).WriteObject(
                            Org.BouncyCastle.Security.DotNetUtilities.FromX509Certificate(cert));

                        if (includePrivateKey)
                        {
                            // 3) pick the right private-key export path
                            byte[] keyDer;
                            string pemLabel;
                            if (password.IsEmpty)
                            {
                                // unencrypted PKCS#8
                                keyDer = cert.GetRSAPrivateKey() is RSA rsa
                                           ? rsa.ExportPkcs8PrivateKey()
                                           : cert.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey();
                                pemLabel = "PRIVATE KEY";
                            }
                            else
                            {
                                // encrypted PKCS#8
                                var pbe = new PbeParameters(
                                    PbeEncryptionAlgorithm.Aes256Cbc,
                                    HashAlgorithmName.SHA256,
                                    100_000
                                );

                                keyDer = cert.GetRSAPrivateKey() is RSA rsaEnc
                                           ? rsaEnc.ExportEncryptedPkcs8PrivateKey(password, pbe)
                                           : cert.GetECDsaPrivateKey()!
                                                .ExportEncryptedPkcs8PrivateKey(password, pbe);
                                pemLabel = "ENCRYPTED PRIVATE KEY";

                            }
                            // 2) Wrap that DER in PEM *correctly*:
                            string keyPem = PemEncoding.WriteString(pemLabel, keyDer);
                            string keyFilePath = Path.GetFileNameWithoutExtension(filePath) + ".key";
                            // 3) Write the .key file
                            File.WriteAllText(keyFilePath, keyPem);
                        }
                        break;
                    }
            }
        }
        finally
        {
            // scrub the char[] copy
            if (pwdChars is not null)
                Array.Clear(pwdChars, 0, pwdChars.Length);
        }
    }

    // 2) The SecureString overload just calls (1) in a callback
    /// <summary>
    /// Exports a certificate using a <see cref="SecureString"/> password.
    /// </summary>
    /// <param name="cert">Certificate to export.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="fmt">Export format.</param>
    /// <param name="password">Password to protect the exported key.</param>
    /// <param name="includePrivateKey">Whether to include the private key in PEM exports.</param>
    public static void Export(
        X509Certificate2 cert,
        string filePath,
        ExportFormat fmt,
        SecureString password,
        bool includePrivateKey = false)
    {
        password.ToSecureSpan(span =>
            // this will run your span‐based implementation,
            // then immediately zero & free the unmanaged buffer
            Export(cert, filePath, fmt, span, includePrivateKey)
        );
    }

    #endregion

    #region  Validation helpers (Test-PodeCertificate equivalent)
    /// <summary>
    /// Validates a certificate against basic criteria such as expiration, self-signing, key usage and revocation.
    /// </summary>
    /// <param name="cert">Certificate to validate.</param>
    /// <param name="checkRevocation">Whether to check revocation online.</param>
    /// <param name="allowWeakAlgorithms">Allow SHA-1 or short keys.</param>
    /// <param name="denySelfSigned">Reject self-signed certificates.</param>
    /// <param name="expectedPurpose">Optional expected enhanced key usages.</param>
    /// <param name="strictPurpose">Require the EKU set to match exactly.</param>
    /// <returns><c>true</c> if the certificate is valid.</returns>
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
        bool isSelfSigned = cert.Subject == cert.Issuer;
        if (denySelfSigned && isSelfSigned)
            return false;

        // ── 3. Chain building  + optional revocation ──────────────────
        using (var chain = new X509Chain())
        {
            chain.ChainPolicy.RevocationMode =
                checkRevocation ? X509RevocationMode.Online
                                : X509RevocationMode.NoCheck;

            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EndCertificateOnly;
            chain.ChainPolicy.DisableCertificateDownloads = !checkRevocation;

            // Allow untrusted root when we’re not denying self-signed
            if (isSelfSigned)
            {
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            }
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


    /// <summary>
    /// Returns the friendly names of the enhanced key usages on the certificate.
    /// </summary>
    /// <param name="cert">Certificate to inspect.</param>
    /// <returns>Sequence of EKU names.</returns>
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

    #region  SecureString extension for ReadOnlySpan<char>
    public unsafe delegate void SpanHandler(ReadOnlySpan<char> span);

    /// <summary>
    /// Converts a <see cref="SecureString"/> into a <see cref="ReadOnlySpan{T}"/> and
    /// invokes the provided <paramref name="handler"/> with that span. The unmanaged
    /// memory backing the span is zeroed and freed immediately after the handler
    /// returns.
    /// </summary>
    /// <param name="secureString">The secure string to expose as a span.</param>
    /// <param name="handler">Delegate that processes the characters of the secure string.</param>
    /// <remarks>
    /// This method allocates unmanaged memory to expose the contents of the
    /// <see cref="SecureString"/>. The buffer is cleared even if the handler throws.
    /// </remarks>
    public static unsafe void ToSecureSpan(this SecureString secureString, SpanHandler handler)
    {
        Log.Debug("Converting SecureString to ReadOnlySpan<char> for handler {Handler}", handler.Method.Name);

        ArgumentNullException.ThrowIfNull(secureString);
        ArgumentNullException.ThrowIfNull(handler);
        if (secureString.Length == 0)
            throw new ArgumentException("SecureString is empty", nameof(secureString));
        // Convert SecureString to a ReadOnlySpan<char> using a pointer
        // This is safe because SecureString guarantees that the memory is zeroed after use.
        IntPtr ptr = IntPtr.Zero;
        try
        {
            // Convert SecureString to a pointer
            // Marshal.SecureStringToCoTaskMemUnicode returns a pointer to the unmanaged memory
            // that contains the characters of the SecureString.
            // This memory must be freed after use to avoid memory leaks.
            Log.Debug("Marshalling SecureString to unmanaged memory");
            ptr = Marshal.SecureStringToCoTaskMemUnicode(secureString);
            var span = new ReadOnlySpan<char>((char*)ptr, secureString.Length);
            handler(span);
            Log.Debug("Handler executed successfully with SecureString span");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while converting SecureString to ReadOnlySpan<char>");
            throw; // rethrow the exception for further handling
        }
        finally
        {
            // Ensure the unmanaged memory is zeroed and freed
            Log.Debug("Zeroing and freeing unmanaged memory for SecureString");
            if (ptr != IntPtr.Zero)
            {
                // zero & free
                for (int i = 0; i < secureString.Length; i++)
                    Marshal.WriteInt16(ptr, i * 2, 0);
                Marshal.ZeroFreeCoTaskMemUnicode(ptr);
            }
        }
    }
    /// <summary>
    /// Creates a new <see cref="SecureString"/> from the characters contained in
    /// the provided <paramref name="span"/>.
    /// </summary>
    /// <param name="span">The characters to copy into the secure string.</param>
    /// <returns>A read-only <see cref="SecureString"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="span"/> is empty.</exception>
    public static SecureString ToSecureString(this ReadOnlySpan<char> span)
    {
        if (span.Length == 0)
            throw new ArgumentException("Span is empty", nameof(span));

        var secure = new SecureString();
        foreach (char c in span)
            secure.AppendChar(c);

        secure.MakeReadOnly();
        return secure;
    }

    #endregion



}
