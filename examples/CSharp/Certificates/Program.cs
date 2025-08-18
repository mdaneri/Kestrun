using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Kestrun;
using Org.BouncyCastle.OpenSsl;   // Only for writing the CSR key
using Org.BouncyCastle.Asn1.X509;
using Kestrun.Utilities;
using Kestrun.Scripting;
using Kestrun.Certificates;
using Kestrun.Logging;
using Kestrun.Hosting;

class Program
{
    static void Main()
    {
        Directory.CreateDirectory("out");

        // ────────────────────────────────────────────────────────────────
        // 1) Generate a self-signed RSA “dev” cert
        // ────────────────────────────────────────────────────────────────
        var rsaCert = CertificateManager.NewSelfSigned(
            new CertificateManager.SelfSignedOptions(
                DnsNames: new[] { "localhost", "127.0.0.1" },
                KeyType: CertificateManager.KeyType.Rsa,
                KeyLength: 2048,
                ValidDays: 30,
                Exportable: true
            ));

        Console.WriteLine($"[RSA] Thumbprint : {rsaCert.Thumbprint}");
        Console.WriteLine($"[RSA] Subject    : {rsaCert.Subject}");
        Console.WriteLine($"[RSA] NotAfter   : {rsaCert.NotAfter:yyyy-MM-dd}");

        // ────────────────────────────────────────────────────────────────
        // 1a) Export DER (.cer) – public only
        // ────────────────────────────────────────────────────────────────
        File.WriteAllBytes("out/devcert.cer",
            rsaCert.Export(X509ContentType.Cert));
        Console.WriteLine("[DER] out/devcert.cer written");

        // ────────────────────────────────────────────────────────────────
        // 1b) Export PFX (span-based) with private key + password
        // ────────────────────────────────────────────────────────────────
        var pwdSpan = "MyP@ssw0rd".AsSpan();
        CertificateManager.Export(
            rsaCert,
            filePath: "out/devcert",
            fmt: CertificateManager.ExportFormat.Pfx,
            password: pwdSpan,
            includePrivateKey: true);
        Console.WriteLine("[Export PFX] out/devcert.pfx written");

        // ────────────────────────────────────────────────────────────────
        // 1c) Export PEM **unencrypted** (plain PRIVATE KEY)  
        // ────────────────────────────────────────────────────────────────
        CertificateManager.Export(
            cert: rsaCert,
            filePath: "out/devcert-plain",
            fmt: CertificateManager.ExportFormat.Pem,
            // no password → unencrypted key
            includePrivateKey: true
        );
        Console.WriteLine("[Export PEM-plain] out/devcert-plain.crt + .key");

        // ────────────────────────────────────────────────────────────────
        // 1d) Export PEM **encrypted** (ENCRYPTED PRIVATE KEY)  
        // ────────────────────────────────────────────────────────────────
        CertificateManager.Export(
            cert: rsaCert,
            filePath: "out/devcert-enc",
            fmt: CertificateManager.ExportFormat.Pem,
            password: pwdSpan,
            includePrivateKey: true
        );
        Console.WriteLine("[Export PEM-enc]   out/devcert-enc.crt + .key");


        // ────────────────────────────────────────────────────────────────
        // 1e) Export PEM *without* private key (cert-only)
        // ────────────────────────────────────────────────────────────────
        CertificateManager.Export(
            rsaCert,
            filePath: "out/devcert-certonly",
            fmt: CertificateManager.ExportFormat.Pem,
            includePrivateKey: false);
        Console.WriteLine("[Export PEM cert-only] out/devcert-certonly.crt written");

        // ────────────────────────────────────────────────────────────────
        // 1f) Export PFX *via* SecureString + ToSecureSpan
        // ────────────────────────────────────────────────────────────────
        var securePwd = new SecureString();
        foreach (char c in "MyP@ssw0rd")
        {
            securePwd.AppendChar(c);
        }

        securePwd.MakeReadOnly();

        CertificateManager.Export(
            rsaCert,
            filePath: "out/devcert-secure",
            fmt: CertificateManager.ExportFormat.Pfx,
            password: securePwd,
            includePrivateKey: true);
        Console.WriteLine("[Export PFX SecureString] out/devcert-secure.pfx written");

        // ────────────────────────────────────────────────────────────────
        // 2) Generate an ECDSA CSR
        // ────────────────────────────────────────────────────────────────
        var (csrPem, privKey) = CertificateManager.NewCertificateRequest(
            new CertificateManager.CsrOptions(
                DnsNames: new[] { "example.com", "www.example.com" },
                KeyType: CertificateManager.KeyType.Ecdsa,
                KeyLength: 384,
                Country: "US",
                Org: "Acme Ltd.",
                CommonName: "example.com"
            ));

        File.WriteAllText("out/example.csr", csrPem);
        using (var sw = new StreamWriter("out/example.key"))
        {
            new PemWriter(sw).WriteObject(privKey);
        }

        Console.WriteLine("[CSR] out/example.csr + out/example.key written");

        // ────────────────────────────────────────────────────────────────
        // 3) Import — demonstrate *all* your Import overloads
        // ────────────────────────────────────────────────────────────────


        // 3a) Import PFX via ReadOnlySpan<char>
        var impPfxSpan = CertificateManager.Import("out/devcert.pfx", pwdSpan);
        Console.WriteLine($"[Import PFX span]    Thumbprint: {impPfxSpan.Thumbprint}");

        // 3b) Import PFX via SecureString
        var impPfxSecure = CertificateManager.Import("out/devcert.pfx", securePwd);
        Console.WriteLine($"[Import PFX secure]  Thumbprint: {impPfxSecure.Thumbprint}");

        // 3c) Import DER public-only
        var impDer = CertificateManager.Import("out/devcert.cer");
        Console.WriteLine($"[Import DER cert]    Subject   : {impDer.Subject}");

        // 3d) Import PEM split (cert + key files)  **plain** PEM (no password)
        var impPemPlain = CertificateManager.Import(
            certPath: "out/devcert-plain.crt",
            password: ReadOnlySpan<char>.Empty,
            privateKeyPath: "out/devcert-plain.key"
        );
        Console.WriteLine($"[Import PEM plain] Thumbprint: {impPemPlain.Thumbprint}");

        // 3e) Import PEM split (cert + key files)   **encrypted** PEM (with password)
        var impPemEnc = CertificateManager.Import(
            certPath: "out/devcert-enc.crt",
            password: pwdSpan,
            privateKeyPath: "out/devcert-enc.key"
        );
        Console.WriteLine($"[Import PEM enc]   Thumbprint: {impPemEnc.Thumbprint}");

        // 3f) Import PEM cert-only
        var impPemCertOnly = CertificateManager.Import("out/devcert-certonly.crt");
        Console.WriteLine($"[Import PEM only]    Subject   : {impPemCertOnly.Subject}");

        // ────────────────────────────────────────────────────────────────
        // 4) Validate *all* imported certs
        // ────────────────────────────────────────────────────────────────
        void ValidateAndReport(string label, X509Certificate2 cert)
        {
            bool ok = CertificateManager.Validate(
                cert,
                checkRevocation: false,
                denySelfSigned: false,
                allowWeakAlgorithms: false
            );
            Console.WriteLine($"[Validate {label,-12}] {ok}");
        }

        ValidateAndReport("PFX span", impPfxSpan);
        ValidateAndReport("PFX secure", impPfxSecure);
        ValidateAndReport("DER cert", impDer);
        ValidateAndReport("PEM plain", impPemPlain);
        ValidateAndReport("PEM enc", impPemEnc);
        ValidateAndReport("PEM cert-only", impPemCertOnly);

        // ────────────────────────────────────────────────────────────────
        // 5) Show EKUs for one example
        // ────────────────────────────────────────────────────────────────
        Console.WriteLine("[EKU]    " +
            string.Join(", ", CertificateManager.GetPurposes(impPfxSpan)));
    }
}
