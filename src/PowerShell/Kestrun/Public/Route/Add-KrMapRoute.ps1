
function Add-KrMapRoute {
    [CmdletBinding(defaultParameterSetName = "ScriptBlock")]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,
        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs = @([Kestrun.Utilities.HttpVerb]::Get),
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true, ParameterSetName = "ScriptBlock")]
        [ScriptBlock]$ScriptBlock,
        [Parameter(Mandatory = $true, ParameterSetName = "Code")]
        [string]$Code,
        [Parameter(Mandatory = $true, ParameterSetName = "Code")]
        [Kestrun.ScriptLanguage]$Language,
        [Parameter()]
        [string[]]$ExtraImports = $null,
        [Parameter()]
        [System.Reflection.Assembly[]]$ExtraRefs = $null

    )
    process {

        $options = [Kestrun.Hosting.MapRouteOptions]::new()
        $options.HttpVerbs = $Verbs
        $options.Pattern = $Path
        $options.ExtraImports = $ExtraImports
        $options.ExtraRefs = $ExtraRefs

        if ($PSCmdlet.ParameterSetName -eq "Code") {
            #  $Server.AddMapRoute($Path, $Verbs, $Code, $Language, $ExtraImports, $ExtraRefs)
            $options.Language = $Language
            $options.ScriptBlock = $Code
        }
        else {
            $options.Language = [Kestrun.ScriptLanguage]::PowerShell
            $options.ScriptBlock = $ScriptBlock.ToString()
        }
        $Server.AddMapRoute($options)
    }
}
