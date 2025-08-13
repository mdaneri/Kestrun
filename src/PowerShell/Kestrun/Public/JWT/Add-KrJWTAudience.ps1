function Add-KrJWTAudience {
    <#
    .SYNOPSIS
        Adds an audience to the JWT token builder.
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken?view=azure-dotnet
    .DESCRIPTION
        This function adds an audience to the JWT token builder, allowing for the specification of the token's audience.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Audience
        The audience to set for the JWT token.
    .OUTPUTS
        [Kestrun.Security.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Add-KrJWTAudience -Audience "myAudience"
        This example creates a new JWT token builder and adds an audience to it.
    .NOTES
        This function is part of the Kestrun.Security module and is used to build JWT tokens
        Maps to JwtTokenBuilder.WithAudience
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken?view=azure-dotnet
    #>
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Security.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Audience
    )
    process { 
        return $Builder.WithAudience($Audience) 
    }
}
