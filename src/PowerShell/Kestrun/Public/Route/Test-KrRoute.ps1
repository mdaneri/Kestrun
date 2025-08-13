function Test-KrRoute {
    <#
    .SYNOPSIS
        Tests if a route exists in the Kestrun host.
    .PARAMETER Path
        The path of the route to test.
    .PARAMETER Verbs
        The HTTP verb(s) to test for the route.
    .EXAMPLE
        Test-KrRoute -Path "/api/test" -Verbs "GET"
        # Tests if a GET route exists for "/api/test".
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage routes.
    #>
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs = @([Kestrun.Utilities.HttpVerb]::Get)
    )
    # Ensure the server instance is resolved
    $Server = Resolve-KestrunServer -Server $Server

    return [Kestrun.Hosting.KestrunHostMapExtensions]::MapExists($Server, $Path, $Verbs)
}