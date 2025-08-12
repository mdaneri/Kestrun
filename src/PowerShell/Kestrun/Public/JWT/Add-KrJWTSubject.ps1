function Add-KrJWTSubject {
    <#
    .SYNOPSIS
        Adds a subject to the JWT token builder.
    .DESCRIPTION
        This function adds a subject to the JWT token builder, allowing for the specification of the token's subject.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Subject
        The subject to set for the JWT token.
    .OUTPUTS
        [Kestrun.Security.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Add-KrJWTSubject -Subject "mySubject"
        This example creates a new JWT token builder and adds a subject to it.
    .NOTES
        This function is part of the Kestrun.Security module and is used to build JWT tokens
        Maps to JwtTokenBuilder.WithSubject
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken?view=azure-dotnet
    #>
    [KestrunRuntimeApi([KestrunApiContext]::Everywhere)]
    [CmdletBinding()]
    [OutputType([Kestrun.Security.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Subject
    )
    process {
        return $Builder.WithSubject($Subject)
    }
}
