function Add-KrRouteGroup {
    <#
    .SYNOPSIS
        Creates a grouped route context (prefix + shared options) for nested Add-KrMapRoute calls.
    .DESCRIPTION
        While the ScriptBlock runs, all Add-KrMapRoute calls inherit:
          - Prefix (prepended to -Path)
          - AuthorizationSchema / AuthorizationPolicy
          - ExtraImports / ExtraRefs
          - Arguments (merged; child overrides keys)
        Supports nesting; inner groups inherit and can override unless -NoInherit is used.
    .PARAMETER Prefix
        The path prefix for the group (e.g. '/todoitems').
    .PARAMETER AuthorizationSchema
        Authorization schemes required by all routes in the group.
    .PARAMETER AuthorizationPolicy
        Authorization policies required by all routes in the group.
    .PARAMETER ExtraImports
        Extra namespaces added to all routes in the group.
    .PARAMETER ExtraRefs
        Extra assemblies referenced by all routes in the group.
    .PARAMETER Arguments
        Extra arguments injected into all routes in the group.
    .PARAMETER ScriptBlock
        Scriptblock within which you call Add-KrMapRoute for relative paths.
    .PARAMETER FileName
        Path to a script file containing the scriptblock to execute.
    .PARAMETER NoInherit
        If set, do not inherit options from the parent group; only apply the current parameters.
    .EXAMPLE
        Add-KrRouteGroup -Prefix '/todoitems' -AuthorizationPolicy 'RequireUser' -ScriptBlock {
            Add-KrMapRoute -Verbs Get  -Path '/'      -ScriptBlock { 'all todos' }
            Add-KrMapRoute -Verbs Get  -Path '/{id}'  -ScriptBlock { "todo $($Context.Request.RouteValues['id'])" }
            Add-KrMapRoute -Verbs Post -Path '/'      -ScriptBlock { write-KrResponse -InputObject 'create' }
        }
        Adds a new route group to the specified Kestrun server with the given prefix and options.
    .EXAMPLE
        Add-KrRouteGroup -Prefix '/todoitems' -FileName 'C:\Scripts\TodoItems.ps1'
        Add the new route group defined in the specified file.
    #>
    [CmdletBinding(DefaultParameterSetName = "ScriptBlock", PositionalBinding = $true)]
    param(
        [Parameter(Mandatory)]
        [string]$Prefix,

        [Parameter()]
        [string[]]$AuthorizationSchema,

        [Parameter()]
        [string[]]$AuthorizationPolicy,

        [Parameter()]
        [string[]]$ExtraImports,

        [Parameter()]
        [System.Reflection.Assembly[]]$ExtraRefs,

        [Parameter()]
        [hashtable]$Arguments,

        [Parameter(Mandatory, Position = 0, ParameterSetName = "ScriptBlock")]
        [ScriptBlock]$ScriptBlock,

        [Parameter(Mandatory, ParameterSetName = "FileName")]
        [string]$FileName,

        [Parameter()]
        [switch]$NoInherit
    )

    if ($PSCmdlet.ParameterSetName -eq 'FileName') {
        if (-not (Test-Path -Path $FileName)) {
            throw "The specified file path does not exist: $FileName"
        }
        $code = Get-Content -Path $FileName -Raw
        $ScriptBlock = [ScriptBlock]::Create($code)
    }

    # Normalize prefix: allow "todoitems" or "/todoitems"
    if (-not [string]::IsNullOrWhiteSpace($Prefix) -and -not $Prefix.StartsWith('/')) {
        $Prefix = "/$Prefix"
    }

    # Compute inheritance
    $parent = if (-not $NoInherit -and $script:KrRouteGroupStack.Count) {
        $script:KrRouteGroupStack.Peek()
    }
    else {
        @{
            Prefix              = ''
            AuthorizationSchema = @()
            AuthorizationPolicy = @()
            ExtraImports        = @()
            ExtraRefs           = @()
            Arguments           = @{}
        }
    }

    $current = @{
        Prefix              = _KrJoin-Route $parent.Prefix $Prefix
        AuthorizationSchema = _KrMerge-Unique $parent.AuthorizationSchema $AuthorizationSchema
        AuthorizationPolicy = _KrMerge-Unique $parent.AuthorizationPolicy $AuthorizationPolicy
        ExtraImports        = _KrMerge-Unique $parent.ExtraImports        $ExtraImports
        ExtraRefs           = @($parent.ExtraRefs + $ExtraRefs)
        Arguments           = _KrMerge-Args    $parent.Arguments          $Arguments
    }

    # If executing from a file, temporarily set location to the file’s directory
    $restorePath = $null
    if ($PSCmdlet.ParameterSetName -eq 'FileName') {
        $restorePath = Get-Location
        Set-Location (Split-Path -Path $FileName -Parent)
    }

    $script:KrRouteGroupStack.Push($current)
    try {
        & $ScriptBlock
    }
    catch {
        $msg = "Error inside route group '$($current.Prefix)': $($_.Exception.Message)"
        throw [System.Exception]::new($msg, $_.Exception)
    }
    finally {
        $null = $script:KrRouteGroupStack.Pop()
        if ($restorePath) { Set-Location $restorePath }
    }
}
