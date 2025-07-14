using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KestrunLib;                                     // <<— helper namespace
using Org.BouncyCastle.OpenSsl;                       // only for writing the CSR key

class Program
{
    static void Main()
    {
        Directory.CreateDirectory("out");

        // ────────────────────────────────────────────────────────────────
        // 1)  RSA self-signed (dev cert)
        // ────────────────────────────────────────────────────────────────
        var rsaCert = CertificateManager.NewSelfSigned(
            new CertificateManager.SelfSignedOptions(
                DnsNames: new[] { "localhost", "127.0.0.1" },
                KeyType: CertificateManager.KeyType.Rsa,
                KeyLength: 2048,
                ValidDays: 30,
                Exportable: true));

        Console.WriteLine($"[RSA] Thumbprint  : {rsaCert.Thumbprint}");
        Console.WriteLine($"[RSA] Subject     : {rsaCert.Subject}");
        Console.WriteLine($"[RSA] NotAfter    : {rsaCert.NotAfter:yyyy-MM-dd}");

        // 1a) export PFX + PEM
        CertificateManager.Export(cert: rsaCert, filePath: "out/devcert",
            fmt: CertificateManager.ExportFormat.Pfx, password: "MyP@ssw0rd".AsSpan(), includePrivateKey: true);

        CertificateManager.Export(cert: rsaCert, filePath: "out/devcert",
            fmt: CertificateManager.ExportFormat.Pem, password: "MyP@ssw0rd".AsSpan(), includePrivateKey: true);

        // ────────────────────────────────────────────────────────────────
        // 2)  ECDSA CSR  (send to CA later…)
        // ────────────────────────────────────────────────────────────────
        var (csrPem, privKey) = CertificateManager.NewCertificateRequest(
            new CertificateManager.CsrOptions(
                DnsNames: ["example.com", "www.example.com"],
                KeyType: CertificateManager.KeyType.Ecdsa,
                KeyLength: 384,
                Country: "US",
                Org: "Acme Ltd.",
                CommonName: "example.com"));

        File.WriteAllText("out/example.csr", csrPem);
        using (var sw = new StreamWriter("out/example.key"))
            new PemWriter(sw).WriteObject(privKey);

        Console.WriteLine("[CSR]  out/example.csr written");

        // ────────────────────────────────────────────────────────────────
        // 3)  Import PFX we just wrote and validate it
        // ────────────────────────────────────────────────────────────────
        var imported = CertificateManager.Import("out/devcert.pfx", "MyP@ssw0rd");

        bool valid = CertificateManager.Validate(
            imported,
            checkRevocation: false,        // offline dev box
            denySelfSigned: false,        // it IS self-signed
            allowWeakAlgorithms: false);

        Console.WriteLine($"[Validate] imported PFX → {valid}");

        // ────────────────────────────────────────────────────────────────
        // 4)  Show EKUs (purposes)
        // ────────────────────────────────────────────────────────────────
        Console.WriteLine("[EKU]    " +
            string.Join(", ", CertificateManager.GetPurposes(imported)));

        // ────────────────────────────────────────────────────────────────
        // 5)  Play with strict EKU + revocation flags
        // ────────────────────────────────────────────────────────────────
        var purposes = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }; // ServerAuth

        bool strictOk = CertificateManager.Validate(
            imported,
            checkRevocation: false,
            denySelfSigned: false,
            expectedPurpose: purposes,
            strictPurpose: true);

        Console.WriteLine($"[Strict EKU] {strictOk}");
    }
}
