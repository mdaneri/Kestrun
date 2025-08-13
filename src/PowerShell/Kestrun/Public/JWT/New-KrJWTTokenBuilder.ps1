
function New-KrJWTBuilder {
    <#
    .SYNOPSIS
        Creates a new JWT token builder instance.
    .DESCRIPTION
        This function initializes a new instance of the JwtTokenBuilder class, which is used to construct JWT tokens.
    .EXAMPLE
        # Creates a new JWT token builder instance
        $builder = New-KrJWTBuilder
    .OUTPUTS
        [Kestrun.Security.JwtTokenBuilder]
        A new instance of the JwtTokenBuilder class.
    .NOTES
        This function is part of the Kestrun.Security module and is used to build JWT tokens.
        Maps to JwtTokenBuilder.New
    #>
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([Kestrun.Security.JwtTokenBuilder])]
    param()
    # Create a new JWT token builder instance
    if ($PSCmdlet.ShouldProcess("JwtTokenBuilder", "Create new JWT token builder")) {
        return [Kestrun.Security.JwtTokenBuilder]::New()
    }
}
