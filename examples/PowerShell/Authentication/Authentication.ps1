
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param()
<#
.SYNOPSIS
    Kestrun PowerShell Example: Multi Routes
.DESCRIPTION
    This script demonstrates how to define multiple routes in Kestrun, a PowerShell web server framework.
.EXAMPLE
    .\Authentication.ps1
    This example shows how to set up a Kestrun server with multiple authentication methods, including Basic Authentication, API Key Authentication, and JWT Bearer Token Authentication.
    It also demonstrates how to secure routes using these authentication methods.

    $creds   = "admin:password"
    $basic   = "Basic " + [Convert]::ToBase64String(
                        [Text.Encoding]::ASCII.GetBytes($creds))
    $token   = (Invoke-RestMethod https://localhost:5001/token -SkipCertificateCheck -Headers @{ Authorization = $basic }).access_token
    Invoke-RestMethod https://localhost:5001/secure/ps/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
    Invoke-RestMethod https://localhost:5001/secure/cs/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
 
    Invoke-RestMethod https://localhost:5001/secure/key/simple/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
    Invoke-RestMethod https://localhost:5001/secure/key/ps/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
    Invoke-RestMethod https://localhost:5001/secure/key/cs/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }

    Invoke-RestMethod https://localhost:5001/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

    #>

try {
    # Get the path of the current script
    # This allows the script to be run from any location
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    # Determine the script path and Kestrun module path
    $powerShellExamplesPath = (Split-Path -Parent ($ScriptPath))
    # Determine the script path and Kestrun module path
    $examplesPath = (Split-Path -Parent ($powerShellExamplesPath))
    # Get the parent directory of the examples path
    # This is useful for locating the Kestrun module
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    # Construct the path to the Kestrun module
    # This assumes the Kestrun module is located in the src/PowerShell/Kestr
    $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"
    # Import the Kestrun module from the source path if it exists, otherwise from installed modules
    if (Test-Path -Path $kestrunModulePath -PathType Leaf) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    }
    else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
}
catch {
    Write-Error "Failed to import Kestrun module: $_"
    Write-Error "Ensure the Kestrun module is installed or the path is correct."
    exit 1
}
$logger = New-KrLogger  |
Set-KrMinimumLevel -Value Debug  |
Add-KrSinkFile -Path ".\logs\Authentication.log" -RollingInterval Hour |
Add-KrSinkConsole |
Register-KrLogger   -Name "DefaultLogger" -PassThru -SetAsDefault

New-KrServer -Name "Kestrun Authentication"

if (Test-Path "$ScriptPath\devcert.pfx" ) {
    $cert = Import-KsCertificate -FilePath ".\devcert.pfx" -Password (convertTo-SecureString -String 'p@ss' -AsPlainText -Force)
}
else {
    $cert = New-KsSelfSignedCertificate -DnsName 'localhost' -Exportable
    Export-KsCertificate -Certificate $cert `
        -FilePath "$ScriptPath\devcert" -Format pfx -IncludePrivateKey -Password (convertTo-SecureString -String 'p@ss' -AsPlainText -Force)
}

if (-not (Test-KsCertificate -Certificate $cert )) {
    Write-Error "Certificate validation failed. Ensure the certificate is valid and not self-signed."
    exit 1
}
 
# Example usage:
Set-KrServerOption -DenyServerHeader
 
Set-KrServerLimit -MaxRequestBodySize 10485760 -MaxConcurrentConnections 100 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
# Configure the listener (adjust port, cert path, and password)
Add-KrListener -Port 5001 -IPAddress ([IPAddress]::Loopback) -X509Certificate $cert -Protocols Http1AndHttp2AndHttp3
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

Add-KrResponseCompression -EnableForHttps -MimeTypes @("text/plain", "text/html", "application/json", "application/xml", "application/x-www-form-urlencoded")
Add-KrPowerShellRuntime
Add-Favicon

$BasicPowershellScheme = "PowershellBasic"; 
$BasicCSharpScheme = "CSharpBasic";
$JwtScheme = "Bearer";
$ApiKeySimple = "ApiKeySimple";
$ApiKeyPowerShell = "ApiKeyPowerShell";
$ApiKeyCSharp = "ApiKeyCSharp";
$issuer = "KestrunApi";
$audience = "KestrunClients";

Add-KrBasicAuthentication -Name $BasicPowershellScheme -Realm "Power-Kestrun" -AllowInsecureHttp -ScriptBlock {
    param($username, $password)
    write-KrInformationLog -MessageTemplate "Basic Authentication: User {0} is trying to authenticate." -PropertyValues $username
    if ($username -eq "admin" -and $password -eq "password") {
        $true
    }
    else {
        $false
    }
}

Add-KrBasicAuthentication -Name $BasicCSharpScheme -Realm "CSharp-Kestrun" -AllowInsecureHttp -csCode @"
   // Log.Information("Validating credentials for {Username}", username);
    return username == "admin" && password == "password";
"@

Add-KrApiKeyAuthentication -Name $ApiKeySimple -AllowInsecureHttp -HeaderName "X-Api-Key" -ExpectedKey "my-secret-api-key"



Add-KrApiKeyAuthentication -Name $ApiKeyPowerShell -AllowInsecureHttp -HeaderName "X-Api-Key" -ScriptBlock {
    param(
        [string]$ProvidedKey
    )
    if ($ProvidedKey -eq 'my-secret-api-key') {
        return $true
    }
    else {
        return $false
    }
}


Add-KrApiKeyAuthentication -Name $ApiKeyCSharp -AllowInsecureHttp -HeaderName "X-Api-Key" -csCode @"
    return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key");
    // or use a simple string comparison:
    //return providedKey == "my-secret-api-key";
"@

$JwtKeyHex = "6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0";
$jwtKeyBytes = ([Convert]::FromHexString($JwtKeyHex))
$jwtSecurityKey = [Microsoft.IdentityModel.Tokens.SymmetricSecurityKey]::new($jwtKeyBytes)
Add-KrJwtBearerAuthentication -Name $JwtScheme  -ValidIssuer $issuer -ValidAudience $audience `
    -IssuerSigningKey $jwtSecurityKey -ValidAlgorithms @([Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::HmacSha256) `
    -ClockSkew (New-TimeSpan -Minutes 5) `


# Enable configuration
Enable-KrConfiguration

# Set-KrPythonRuntime

# Add a route with a script block
Add-KrMapRoute -Verbs Get -Path "/secure/ps/hello" -Authorization $BasicPowershellScheme -ScriptBlock {
    if (-not $Context.HttpContext.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }
    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code." -ContentType "text/plain"
}

Add-KrMapRoute -Verbs Get -Path "/secure/cs/hello" -Authorization $BasicCSharpScheme -ScriptBlock {
    if (-not $Context.HttpContext.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }
    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by C# Code." -ContentType "text/plain" 
}


Add-KrMapRoute -Verbs Get -Path "/secure/key/simple/hello" -Authorization $ApiKeySimple -ScriptBlock {
    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated using simple key matching." -ContentType "text/plain"
}
 

Add-KrMapRoute -Verbs Get -Path "/secure/key/ps/hello" -Authorization $ApiKeyPowerShell -ScriptBlock {
 

    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code." -ContentType "text/plain"
}
 
Add-KrMapRoute -Verbs Get -Path "/secure/key/cs/hello" -Authorization $ApiKeyCSharp -ScriptBlock {
 
    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching C# Code." -ContentType "text/plain"
}
 


Add-KrMapRoute -Verbs Get -Path "/secure/jwt/hello" -Authorization $JwtScheme -ScriptBlock {

    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by JWT Bearer Token." -ContentType "text/plain"
}


Add-KrMapRoute -Verbs Get -Path "/token" -Authorization $JwtScheme -ScriptBlock {

    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by JWT Bearer Token." -ContentType "text/plain"
}

# Start the server asynchronously
Start-KrServer -Server $server

