
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
        Invoke-RestMethod https://localhost:5001/secure/ps/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod https://localhost:5001/secure/cs/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod https://localhost:5001/secure/vb/hello -SkipCertificateCheck -Headers @{Authorization=$basic}

        Invoke-RestMethod https://localhost:5001/secure/ps/policy -Method GET -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod https://localhost:5001/secure/ps/policy -Method DELETE -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod https://localhost:5001/secure/ps/policy -Method POST -SkipCertificateCheck -Headers @{Authorization=$basic}

        Invoke-RestMethod https://localhost:5001/secure/vb/policy -Method GET -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod https://localhost:5001/secure/vb/policy -Method DELETE -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod https://localhost:5001/secure/vb/policy -Method POST -SkipCertificateCheck -Headers @{Authorization=$basic}

        Invoke-RestMethod https://localhost:5001/secure/key/simple/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod https://localhost:5001/secure/key/ps/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod https://localhost:5001/secure/key/cs/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod https://localhost:5001/secure/key/vb/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        
        $token = (Invoke-RestMethod https://localhost:5001/token/new -SkipCertificateCheck -Headers @{ Authorization = $basic }).access_token
        Invoke-RestMethod https://localhost:5001/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

        Invoke-RestMethod https://localhost:5001/secure/jwt/policy -Method GET -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
        Invoke-RestMethod https://localhost:5001/secure/jwt/policy -Method DELETE -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
        Invoke-RestMethod https://localhost:5001/secure/jwt/policy -Method POST -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

        $token2 = (Invoke-RestMethod https://localhost:5001/token/renew -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }).access_token
        Invoke-RestMethod https://localhost:5001/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token2" } 
        #Form
        Invoke-WebRequest -Uri https://localhost:5001/cookies/login -SkipCertificateCheck -Method Post -Body @{ username = 'admin'; password = 'secret' } -SessionVariable authSession
        Invoke-WebRequest -Uri https://localhost:5001/cookies/secure -SkipCertificateCheck -WebSession $authSession 

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
$BasicVBNetScheme = "VBNetBasic";
$JwtScheme = "Bearer";
$ApiKeySimple = "ApiKeySimple";
$ApiKeyPowerShell = "ApiKeyPowerShell";
$ApiKeyCSharp = "ApiKeyCSharp";
$ApiKeyVBNet = "ApiKeyVBNet";
$issuer = "KestrunApi";
$audience = "KestrunClients";



$claimConfig = New-KrClaimPolicy |
Add-KrClaimPolicy -PolicyName "CanCreate" -ClaimType "can_create" -AllowedValues "true" |
Add-KrClaimPolicy -PolicyName "CanDelete" -ClaimType "can_delete" -AllowedValues "true" |
Add-KrClaimPolicy -PolicyName "CanRead" -ClaimType "can_read" -AllowedValues "true" |
Add-KrClaimPolicy -PolicyName "CanWrite" -ClaimType "can_write" -AllowedValues "true" |
Add-KrClaimPolicy -PolicyName "Admin" -UserClaimType Role -AllowedValues "admin" |
Build-KrClaimPolicy

Add-KrBasicAuthentication -Name $BasicPowershellScheme -Realm "Power-Kestrun" -AllowInsecureHttp -ScriptBlock {
    param($username, $password)
    write-KrInformationLog -MessageTemplate "Basic Authentication: User {0} is trying to authenticate." -PropertyValues $username
    if ($username -eq "admin" -and $password -eq "password") {
        $true
    }
    else {
        $false
    }
} -IssueClaimsScriptBlock {
    param([string]$Identity)
    if ($Identity -eq 'admin') {
        # Return claims for the admin user
        return (Add-KrUserClaim -UserClaimType Role -Value "admin" |
            Add-KrUserClaim -ClaimType "can_read" -Value "true" |
            Add-KrUserClaim -ClaimType "can_write" -Value "true" |
            Add-KrUserClaim -ClaimType "can_delete" -Value "false")
    }
    else {
        return [System.Security.Claims.Claim[]]@()
    }
} -Logger $logger -ClaimPolicyConfig $claimConfig



Add-KrBasicAuthentication -Name $BasicCSharpScheme -Realm "CSharp-Kestrun" -AllowInsecureHttp -Code @"
   // Log.Information("Validating credentials for {Username}", username);
    return username == "admin" && password == "password";
"@ -CodeLanguage CSharp


Add-KrBasicAuthentication -Name $BasicVBNetScheme -Realm "VBNet-Kestrun" -AllowInsecureHttp -Code @"
    ' Log.Information("Validating credentials for {Username}", username)
    Return username = "admin" AndAlso password = "password"
"@ -CodeLanguage VBNet -IssueClaimsCode @"
    If Identity = "admin" Then          ' (VB is case-insensitive, but keep it consistent)
        Return New System.Security.Claims.Claim() {
            New System.Security.Claims.Claim("can_write", "true")
        }
    End If

    ' everyone else gets no extra claims
    Return Nothing
"@ -IssueClaimsCodeLanguage VBNet -ClaimPolicyConfig $claimConfig -Logger $logger

# ── WINDOWS AUTHENTICATION ────────────────────────────────────────────
Add-KrWindowsAuthentication



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
} -IssueClaimsCode @'
    if (identity == "admin")
    {
        // ← return the claims you want to add
        return new[]
        {
        new System.Security.Claims.Claim("can_read", "true")          // custom claim
        // or, if you really want it as a role:
        // new Claim(ClaimTypes.Role, "can_read")
    };
    }

    // everyone else gets no extra claims
    return Enumerable.Empty<System.Security.Claims.Claim>();
'@ -ClaimPolicyConfig $claimConfig -Logger $logger


Add-KrApiKeyAuthentication -Name $ApiKeyCSharp -AllowInsecureHttp -HeaderName "X-Api-Key" -Code @"
    return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key");
    // or use a simple string comparison:
    //return providedKey == "my-secret-api-key";
"@

Add-KrApiKeyAuthentication -Name $ApiKeyVBNet -AllowInsecureHttp -HeaderName "X-Api-Key" -Code @"
    Return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key")
    ' or use a simple string comparison:
    ' Return providedKey = "my-secret-api-key"
"@ -CodeLanguage VBNet



######TODO: Add more authentication methods like OAuth, OpenID Connect, etc.

$secretB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('my-passphrase'))  # or any base64url

$JwtKeyHex = "6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0";
$JwtTokenBuilder = New-KrJWTBuilder |
Add-KrJWTIssuer    -Issuer   $issuer |
Add-KrJWTAudience  -Audience $audience |
#| Set-JwtSubject   -Subject  'admin' `
Protect-KrJWT -HexadecimalKey $JwtKeyHex -Algorithm HS256 

$result = Build-KrJWT -Builder $JwtTokenBuilder
#$jwt     = Get-JwtToken -Result $result
$jwtOptions = $result | Get-KrJWTValidationParameter

Add-KrJWTBearerAuthentication -Name $JwtScheme -ValidationParameter $jwtOptions -ClaimPolicy $claimConfig
<#
$JwtKeyHex = "6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0";
$jwtKeyBytes = ([Convert]::FromHexString($JwtKeyHex))
$jwtSecurityKey = [Microsoft.IdentityModel.Tokens.SymmetricSecurityKey]::new($jwtKeyBytes)
Add-KrJWTBearerAuthentication -Name $JwtScheme  -ValidIssuer $issuer -ValidAudience $audience `
    -IssuerSigningKey $jwtSecurityKey -ValidAlgorithms @([Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::HmacSha256) `
    -ClockSkew (New-TimeSpan -Minutes 5) `
#>

# Enable configuration
Enable-KrConfiguration

<#
***************************************************************************************
    ROUTES
    ──────────────────────────────────────────
    These routes are protected by the authentication schemes defined above.
    They will only be accessible if the user is authenticated.
****************************************************************************************
#>

# Add a route with a script block
Add-KrMapRoute -Verbs Get -Path "/secure/ps/hello" -AuthorizationSchema $BasicPowershellScheme -ScriptBlock {
    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code." -ContentType "text/plain"
}

Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/ps/policy"
        HttpVerbs       = 'Get'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_read' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanRead")
        RequireSchemes  = @($BasicPowershellScheme)
    })


Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/ps/policy"
        HttpVerbs       = 'Delete'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_delete' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanDelete")
        RequireSchemes  = @($BasicPowershellScheme)
    })

Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/ps/policy"
        HttpVerbs       = 'Post', 'Put'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_write' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanWrite")
        RequireSchemes  = @($BasicPowershellScheme)
    })


Add-KrMapRoute -Verbs Get -Path "/secure/cs/hello" -AuthorizationSchema $BasicCSharpScheme -ScriptBlock {
    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by C# Code." -ContentType "text/plain" 
}

Add-KrMapRoute -Verbs Get -Path "/secure/vb/hello" -AuthorizationSchema $BasicVBNetScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code." -ContentType "text/plain"
}



Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/vb/policy"
        HttpVerbs       = 'Get'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_read' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanRead")
        RequireSchemes  = @($BasicVBNetScheme)
    })


Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/vb/policy"
        HttpVerbs       = 'Delete'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_delete' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanDelete")
        RequireSchemes  = @($BasicVBNetScheme)
    })

Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/vb/policy"
        HttpVerbs       = 'Post', 'Put'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_write' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanWrite")
        RequireSchemes  = @($BasicVBNetScheme)
    })






Add-KrMapRoute -Verbs Get -Path "/secure/key/simple/hello" -AuthorizationSchema $ApiKeySimple -ScriptBlock {
    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated using simple key matching." -ContentType "text/plain"
}
 

Add-KrMapRoute -Verbs Get -Path "/secure/key/ps/hello" -AuthorizationSchema $ApiKeyPowerShell -ScriptBlock {
    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code." -ContentType "text/plain"
}
 
Add-KrMapRoute -Verbs Get -Path "/secure/key/cs/hello" -AuthorizationSchema $ApiKeyCSharp -ScriptBlock {
 
    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching C# Code." -ContentType "text/plain"
}

Add-KrMapRoute -Verbs Get -Path "/secure/key/Vb/hello" -AuthorizationSchema $ApiKeyVB -ScriptBlock {

    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching VB.NET Code." -ContentType "text/plain"
}



# KESTRUN JWT AUTHENTICATION ROUTES 

Add-KrMapRoute -Verbs Get -Path "/secure/jwt/hello" -AuthorizationSchema $JwtScheme -ScriptBlock {

    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by JWT Bearer Token." -ContentType "text/plain"
}

Add-KrMapRoute -Verbs Get -Path "/secure/jwt/policy" -AuthorizationSchema $JwtScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_read' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanRead"

Add-KrMapRoute -Verbs Post,Put -Path "/secure/jwt/policy" -AuthorizationSchema $JwtScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_write' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanWrite"

Add-KrMapRoute -Verbs Delete -Path "/secure/jwt/policy" -AuthorizationSchema $JwtScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_delete' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanDelete"


Add-KrMapRoute -Verbs Get -Path "/token/renew" -AuthorizationSchema $JwtScheme  -ScriptBlock {
    $user = $Context.HttpContext.User.Identity.Name

    write-KrInformationLog -MessageTemplate "Generating JWT token for user {0}" -PropertyValues $user 
  
    Write-Output "JwtTokenBuilder Type : $($JwtBuilderResult.GetType().FullName)"
    Write-Output "IssuedAt : $($JwtBuilderResult.IssuedAt)"
    Write-Output "Expires : $($JwtBuilderResult.Expires)"
 
    $accessToken = $JwtBuilderResult | Update-KrJWT
    Write-KrJsonResponse -InputObject @{
        access_token = $accessToken
        token_type   = "Bearer"
        expires_in   = $build.Expires
    } -ContentType "application/json"

} -Arguments @{"JwtBuilderResult" = $JwtTokenBuilder |  Build-KrJWT }


Add-KrMapRoute -Verbs Get -Path "/token/new" -AuthorizationSchema $BasicPowershellScheme -ScriptBlock {
    $user = $Context.HttpContext.User.Identity.Name

    write-KrInformationLog -MessageTemplate "Regenerating JWT token for user {0}" -PropertyValues $user
    write-KrInformationLog -MessageTemplate "JWT Token Builder: {0}" -PropertyValues $JwtTokenBuilder
    if (-not $JwtTokenBuilder) {
        Write-KrErrorResponse -Message "JWT Token Builder is not initialized." -StatusCode 500
        return
    } 
    Write-Output "JwtTokenBuilder Type : $($JwtTokenBuilder.GetType().FullName)"
    Write-Output "Issuer : $($JwtTokenBuilder.Issuer)"
    Write-Output "Audience : $($JwtTokenBuilder.Audience)"
    Write-Output "Algorithm: $($JwtTokenBuilder.Algorithm)" 

    $build = Add-KrJWTSubject -Builder $JwtTokenBuilder -Subject $user | Add-KrJWTClaim -UserClaimType Role -Value "admin" |
    Add-KrJWTClaim -ClaimType "can_read" -Value "true" | Build-KrJWT
    $accessToken = $build | Get-KrJWTToken
    Write-KrJsonResponse -InputObject @{
        access_token = $accessToken
        token_type   = "Bearer"
        expires_in   = $build.Expires
    } -ContentType "application/json"

} -Arguments @{"JwtTokenBuilder" = $JwtTokenBuilder }

# Start the server asynchronously
Start-KrServer -Server $server

