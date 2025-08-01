function Update-KrJWT {
    <#
    .SYNOPSIS
        Updates an existing JWT token.
    .DESCRIPTION
        This function allows you to update an existing JWT token by renewing it with a new expiration time.
    .PARAMETER Result
        The JWT builder result containing the token to update.
    .PARAMETER Lifetime
        The new duration for which the JWT token will be valid.
    .EXAMPLE
        # Updates the JWT token with a new expiration time
        $token = Get-KrJWT -Name "MyToken"
        $newToken = Update-KrJWT -Result $token -Lifetime (New-TimeSpan -Minutes 30)
    .OUTPUTS
        [string]
        The updated JWT token.
    .NOTES
        This function is part of the Kestrun.Security module and is used to manage JWT tokens.
        Maps to JwtBuilderResult.Renew
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken?view=azure-dotnet
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytokenhandler?view=azure-dotnet
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtBuilderResult] $Result,
        [Parameter()]
        [TimeSpan] $Lifetime
    )
    process {
        if ($PSCmdlet.ShouldProcess("JWT token", "Renew token with new lifetime")) {
            return $Result.Renew($Lifetime)
        }
    }
}
