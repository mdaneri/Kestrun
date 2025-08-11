function Update-KrJWT {
    <#
    .SYNOPSIS
        Updates an existing JWT token.
    .DESCRIPTION
        This function updates an existing JWT token by renewing it with a new lifetime.
        It can either take a token directly or extract it from the current HTTP context.
    .PARAMETER Builder
        The JWT token builder used to renew the token.
    .PARAMETER Lifetime
        The new duration for which the JWT token will be valid.
    .PARAMETER Token
        The existing JWT token to update.
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
    [CmdletBinding(SupportsShouldProcess = $true, defaultParameterSetName = "Token")]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory = $true, ParameterSetName = "Token")]
        [string]$Token,

        [Parameter(Mandatory = $true, ParameterSetName = "Context")]
        [switch] $FromContext,
        [Parameter()]
        [TimeSpan] $Lifetime
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Token') {
            if ($PSCmdlet.ShouldProcess("JWT token", "Renew token with new lifetime")) {
                return $Builder.RenewJwt($Token, $Lifetime)
            }
        }
        if ($FromContext.IsPresent -and $null -ne $Context.Request) {
            if ($PSCmdlet.ShouldProcess("JWT token", "Renew token with new lifetime")) {
                return $Builder.RenewJwt($Context, $Lifetime)
            }
        }
    }
}
