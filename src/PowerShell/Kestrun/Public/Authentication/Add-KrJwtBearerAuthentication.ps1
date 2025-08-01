function Add-KrJwtBearerAuthentication {
    <#
    .SYNOPSIS
        Adds JWT Bearer authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use JWT Bearer authentication for incoming requests.
    .PARAMETER Server
        The Kestrun server instance to configure.
    #>
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.IdentityModel.Tokens.TokenValidationParameters]$Options,

        [string]  $ValidIssuer,
        [string[]]$ValidIssuers,
        [string]  $ValidAudience,
        [string[]]$ValidAudiences,
        [string[]]$ValidAlgorithms,
        [switch]  $SkipValidateIssuer ,
        [switch]  $SkipValidateAudience ,
        [switch]  $SkipValidateLifetime ,
        [switch]  $ValidateIssuerSigningKey,
        [switch]  $DoesNotRequireExpirationTime ,
        [switch]  $DoesNotRequireSignedTokens,
        [Microsoft.IdentityModel.Tokens.SecurityKey]$IssuerSigningKey,
        [Microsoft.IdentityModel.Tokens.SecurityKey[]]$IssuerSigningKeys,
        [TimeSpan]$ClockSkew,
        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Microsoft.IdentityModel.Tokens.TokenValidationParameters]::new()
            if ($PSBoundParameters.ContainsKey('ValidIssuer')) { $Options.ValidIssuer = $ValidIssuer }
            if ($PSBoundParameters.ContainsKey('ValidIssuers')) { $Options.ValidIssuers = $ValidIssuers }
            if ($PSBoundParameters.ContainsKey('ValidAudience')) { $Options.ValidAudience = $ValidAudience }
            if ($PSBoundParameters.ContainsKey('ValidAudiences')) { $Options.ValidAudiences = $ValidAudiences }
            if ($PSBoundParameters.ContainsKey('ValidAlgorithms')) { $Options.ValidAlgorithms = $ValidAlgorithms }
            if ($PSBoundParameters.ContainsKey('SkipValidateIssuer')) { $Options.ValidateIssuer = -not $SkipValidateIssuer.IsPresent }
            if ($PSBoundParameters.ContainsKey('SkipValidateAudience')) { $Options.ValidateAudience = -not $SkipValidateAudience.IsPresent }
            if ($PSBoundParameters.ContainsKey('SkipValidateLifetime')) { $Options.ValidateLifetime = -not $SkipValidateLifetime.IsPresent }
            if ($PSBoundParameters.ContainsKey('ValidateIssuerSigningKey')) { $Options.ValidateIssuerSigningKey = $ValidateIssuerSigningKey.IsPresent }

            if ($PSBoundParameters.ContainsKey('RequireExpirationTime')) { $Options.RequireExpirationTime = -not $DoesNotRequireExpirationTime.IsPresent }
            if ($PSBoundParameters.ContainsKey('RequireSignedTokens')) { $Options.RequireSignedTokens = -not$DoesNotRequireSignedTokens.IsPresent }

            if ($PSBoundParameters.ContainsKey('IssuerSigningKey')) { $Options.IssuerSigningKey = $IssuerSigningKey }
            if ($PSBoundParameters.ContainsKey('IssuerSigningKeys')) { $Options.IssuerSigningKeys = $IssuerSigningKeys }

            if ($PSBoundParameters.ContainsKey('ClockSkew')) { $Options.ClockSkew = $ClockSkew }

        }
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        [Kestrun.Hosting.KestrunHostAuthExtensions]::AddJwtBearerAuthentication(
            $Server, $Name, $Options, $null, $null        ) | Out-Null
        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}