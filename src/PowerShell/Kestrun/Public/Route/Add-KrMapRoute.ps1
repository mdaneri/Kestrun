
function Add-KrMapRoute {
    <#
    .SYNOPSIS
        Adds a new map route to the Kestrun server.
    .DESCRIPTION
        This function allows you to add a new map route to the Kestrun server by specifying the route path and the script block or code to be executed when the route is accessed.
    .PARAMETER Server
        The Kestrun server instance to which the route will be added.
        If not specified, the function will attempt to resolve the current server context.
    .PARAMETER Verbs
        The HTTP verbs (GET, POST, etc.) that the route should respond to.
    .PARAMETER Path
        The URL path for the new route.
    .PARAMETER ScriptBlock
        The script block to be executed when the route is accessed.
    .PARAMETER Code
        The code to be executed when the route is accessed, used in conjunction with the Language parameter.
    .PARAMETER Language
        The scripting language of the code to be executed.
    .PARAMETER Authorization
        An optional array of authorization strings for the route.
    .PARAMETER ExtraImports
        An optional array of additional namespaces to import for the route.
    .PARAMETER ExtraRefs
        An optional array of additional assemblies to reference for the route.
    .PARAMETER PassThru
        If specified, the function will return the created route object.
    .OUTPUTS
        Returns a Kestrun.Hosting.MapRoute object representing the newly created route if PassThru is specified.
    .EXAMPLE
        Add-KrMapRoute -Server $myServer -Path "/myroute" -ScriptBlock { Write-Host "Hello, World!" }
        Adds a new map route to the specified Kestrun server with the given path and script block.
    .EXAMPLE

        Add-KrMapRoute -Server $myServer -Path "/myroute" -Code "Write-Host 'Hello, World!'" -Language PowerShell
        Adds a new map route to the specified Kestrun server with the given path and code.
    .EXAMPLE
        Get-KrServer | Add-KrMapRoute -Path "/myroute" -ScriptBlock { Write-Host "Hello, World!" } -PassThru
        Adds a new map route to the current Kestrun server and returns the route object.
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage routes
    #>
    [CmdletBinding(defaultParameterSetName = "ScriptBlock")]
    [OutputType([Microsoft.AspNetCore.Builder.RouteHandlerBuilder])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs = @([Kestrun.Utilities.HttpVerb]::Get),

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true, ParameterSetName = "ScriptBlock")]
        [ScriptBlock]$ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = "Code")]
        [string]$Code,

        [Parameter(Mandatory = $true, ParameterSetName = "Code")]
        [Kestrun.Scripting.ScriptLanguage]$Language,

        [Parameter()]
        [string[]]$Authorization = $null,

        [Parameter()]
        [string[]]$ExtraImports = $null,

        [Parameter()]
        [System.Reflection.Assembly[]]$ExtraRefs = $null,

        [Parameter()]
        [hashtable]$Arguments,

        [Parameter()]
        [switch]$PassThru

    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        $options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
        $options.HttpVerbs = $Verbs
        $options.Pattern = $Path
        $options.ExtraImports = $ExtraImports
        $options.ExtraRefs = $ExtraRefs
        $options.RequireAuthorization = $Authorization

        if ($null -ne $Arguments) {
            $dict = [System.Collections.Generic.Dictionary[string, object]]::new()
            foreach ($key in $Arguments.Keys) {
                $dict[$key] = $Arguments[$key]
            }
            $options.Arguments = $dict
        }

        if ($PSCmdlet.ParameterSetName -eq "Code") {
            $options.Language = $Language
            $options.Code = $Code
        }
        else {
            $options.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
            $options.Code = $ScriptBlock.ToString()
        }

        $map = [Kestrun.Hosting.KestrunHostMapExtensions]::AddMapRoute($Server, $options)

        if ($PassThru) {
            return $map
        }
    }
}
