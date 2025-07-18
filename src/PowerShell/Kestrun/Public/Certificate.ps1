
# Short alias to save typing
$Kcm = [Kestrun.CertificateManager]

# ---------------------------------------------------------------------------
function New-KsSelfSignedCertificate {
    <#
.SYNOPSIS
    Generates a self-signed X.509 certificate (RSA or ECDSA).

.EXAMPLE
    $cert = New-KestrunSelfSignedCertificate -DnsName localhost,127.0.0.1 `
                -KeyType Rsa -KeyLength 2048 -ValidDays 30 -Exportable
#>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string[]]  $DnsName,

        [ValidateSet('Rsa', 'Ecdsa')]
        [string]    $KeyType = 'Rsa',

        [ValidateRange(256, 8192)]
        [int]       $KeyLength = 2048,

        [ValidateRange(1, 3650)]
        [int]       $ValidDays = 365,

        [switch]    $Ephemeral,
        [switch]    $Exportable
    )

    $opts = [Kestrun.CertificateManager+SelfSignedOptions]::new(
        $DnsName,
        [Kestrun.CertificateManager+KeyType]::$KeyType,
        $KeyLength,
        $null,      # purposes
        $ValidDays,
        $Ephemeral.IsPresent,
        $Exportable.IsPresent
    )

    if ($PSCmdlet.ShouldProcess("Create self-signed certificate for $($DnsName -join ', ')")) {
        return $Kcm::NewSelfSigned($opts)
    }
}

# ---------------------------------------------------------------------------
function New-KsCertificateRequest {
<#
.SYNOPSIS
    Creates a PEM-encoded CSR (and returns the private key).

.EXAMPLE
    $csr, $priv = New-KestrunCertificateRequest -DnsName 'example.com' -Country US
#>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string[]] $DnsName,

        [ValidateSet('Rsa', 'Ecdsa')]
        [string]   $KeyType = 'Rsa',

        [int]      $KeyLength = 2048,

        [string]   $Country,
        [string]   $Org,
        [string]   $OrgUnit,
        [string]   $CommonName
    )

    $opts = [Kestrun.CertificateManager+CsrOptions]::new(
        $DnsName,
        [Kestrun.CertificateManager+KeyType]::$KeyType,
        $KeyLength,
        $Country,
        $Org,
        $OrgUnit,
        $CommonName
    )

    if ($PSCmdlet.ShouldProcess("Create certificate request for $($DnsName -join ', ')")) {
        return $Kcm::NewCertificateRequest($opts)
    }
}

# ---------------------------------------------------------------------------
function Import-KsCertificate {
<#
.SYNOPSIS
    Imports a PFX/PEM certificate file and returns X509Certificate2.
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [securestring] $Password,
        [string] $PrivateKeyPath
    )
    $resolvedPath = Resolve-KrPath -Path $FilePath -KestrunRoot -Test
    Write-KrLog -level "Verbose" -Message "Resolved file path: $resolvedPath"
    if ($null -eq $Password) {
        return $Kcm::Import($resolvedPath, $PrivateKeyPath)
    }
    return $Kcm::Import($resolvedPath, $Password, $PrivateKeyPath)
}

# ---------------------------------------------------------------------------
function Export-KsCertificate {
<#
.SYNOPSIS
    Exports an X509Certificate2 to PFX or PEM(+key).

.EXAMPLE
    Export-KestrunCertificate -Certificate $cert -FilePath 'C:\certs\my' `
            -Format Pem -Password 'p@ss' -IncludePrivateKey
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [Parameter(Mandatory)][string]$FilePath,

        [ValidateSet('Pfx', 'Pem')]
        [string] $Format = 'Pfx',

        [securestring] $Password,
        [switch] $IncludePrivateKey
    )
    $resolvedPath = Resolve-KrPath -Path $FilePath -KestrunRoot
    Write-KrLog -level "Verbose" -Message "Resolved file path: $resolvedPath"

    $fmtEnum = [Kestrun.CertificateManager+ExportFormat]::$Format
    $Kcm::Export($Certificate, $resolvedPath, $fmtEnum, $Password,
        $IncludePrivateKey.IsPresent)
}

# ---------------------------------------------------------------------------
function Test-KsCertificate {
    <#
.SYNOPSIS
    Validates a certificate’s chain, EKU, and cryptographic strength.

.EXAMPLE
    Test-KestrunCertificate -Certificate $cert -DenySelfSigned -CheckRevocation
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,

        [switch] $CheckRevocation,
        [switch] $AllowWeakAlgorithms,
        [switch] $DenySelfSigned,

        [string[]] $ExpectedPurpose,
        [switch] $StrictPurpose
    )

    $oidColl = if ($ExpectedPurpose) {
        $oc = [System.Security.Cryptography.OidCollection]::new()
        foreach ($p in $ExpectedPurpose) { $oc.Add([System.Security.Cryptography.Oid]::new($p)) }
        $oc
    }
    else { $null }

    return $Kcm::Validate($Certificate,
        $CheckRevocation.IsPresent,
        $AllowWeakAlgorithms.IsPresent,
        $DenySelfSigned.IsPresent,
        $oidColl,
        $StrictPurpose.IsPresent)
}

# ---------------------------------------------------------------------------
function Get-KsCertificatePurpose {
<#
.SYNOPSIS
    Lists the Enhanced Key Usage values on a certificate.
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate
    )

    return $Kcm::GetPurposes($Certificate)
}
 
