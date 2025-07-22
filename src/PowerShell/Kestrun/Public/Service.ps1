
# Helpers ──────────────────────────────────────────────────────────────────
function New-Delegate([Type]$type, [ScriptBlock]$script) {
    return $script.GetNewClosure().GetDelegate($type)
}

# -------------------------------------------------------------------------



function Add-KrPowerShellRuntime {
    <#
.SYNOPSIS
    Mounts raw *.ps1* endpoints.
#>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [Kestrun.KestrunHost]$Server,

        [string]$PathPrefix = $null
    )
    process {
        $psPrefix = if ($PathPrefix) {
            [Microsoft.AspNetCore.Http.PathString]::new($PathPrefix)
        }
        else {
            $null
        }
        $Server.AddPowerShellRuntime($psPrefix) | Out-Null
    }
}

# -------------------------------------------------------------------------
function Add-KrPowerShellRazorPagesRuntime {
    <#
.SYNOPSIS
    Enables *.cshtml + *.cshtml.ps1* duo pages.
#>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [Kestrun.KestrunHost]$Server,

        [string]$PathPrefix = $null
    )
    process {
        $psPrefix = if ($PathPrefix) {
            [Microsoft.AspNetCore.Http.PathString]::new($PathPrefix)
        }
        else {
            $null
        }
        $Server.AddPowerShellRazorPages($psPrefix) | Out-Null
    }
}
function Add-KrRazorPageService {
    <#
.SYNOPSIS
    Registers Razor Pages (optionally with custom conventions).
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.KestrunHost]$Server,
        [Parameter()]
        [string]$RootDirectory = $null,
        [Parameter()]
        [Microsoft.AspNetCore.Mvc.ApplicationModels.IPageConventions[]]$Conventions = @()
 
    )
    process {
        $options = [Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions]::new()
        if ($RootDirectory) {
            $options.RootDirectory = $RootDirectory
        }
        if ($Conventions.Count -gt 0) {
            foreach ( $c in $Conventions) {
                $options.Conventions.Add($c)
            }
        }
        if ($options) {
            $Server.AddRazorPages($options) | out-Null
        }
        else {
            $Server.AddRazorPages() | out-Null
        }
    }
}


function Add-KrHealthCheck {
    <#
.SYNOPSIS
    Publishes a /healthz endpoint (plus optional checks).
#>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [Kestrun.KestrunHost]$Server,

        [string]$Path = "/healthz"
    )
    process { $Server.AddHealthChecks($Path) | Out-Null }
}

function Add-KrControllersService {
    <#
.SYNOPSIS
    Registers MVC / API controllers.
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.KestrunHost]$Server
    )
    process { $Server.AddControllers() | out-Null }
}

# -------------------------------------------------------------------------
function Add-KrResponseCompression {
    <#
.SYNOPSIS
    Enables gzip / Brotli compression (with optional customisation).
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.KestrunHost]$Server,

        [switch]$EnableForHttps,
        [string[]]$MimeTypes
    )
    process {
        $options = [Microsoft.AspNetCore.ResponseCompression.ResponseCompressionOptions]::new()
        if ($null -ne $MimeTypes -and $MimeTypes.Count -gt 0) {
            $options.MimeTypes = $MimeTypes
        }
        if ($EnableForHttps) {
            $options.EnableForHttps = $true
        }
        $Server.AddResponseCompression($options) | Out-Null
    }
}
 

function Add-KrStaticFilesService {
    <#
.SYNOPSIS  Mounts a physical folder as static content (optionally with index.html lookup).
#>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [Kestrun.KestrunHost]$Server,

        [Parameter()]
        [string]$PhysicalPath,

        [Parameter()]
        [string]$RequestPath = '/',
        [Parameter()]
        [switch]$HttpsCompression,
        [Parameter()]
        [switch]$ServeUnknownFileTypes,
        [Parameter()]
        [string]
        $DefaultContentType
    )
    process {
        $Options = [Microsoft.AspNetCore.Builder.StaticFileOptions]$Options::new()
        if ($null -ne $PhysicalPath) {
            $Options.FileProvider = [Microsoft.Extensions.FileProviders.PhysicalFileProvider]::new($PhysicalPath)
        }
        if ($null -ne $Options.RequestPath) {
            $Options.RequestPath = [Microsoft.AspNetCore.Http.PathString]::new($RequestPath)
        }
        if ($ServeUnknownFileTypes.IsPresent) {
            $Options.ServeUnknownFileTypes = $true
        }
        if ($HttpsCompression.IsPresent) {
            $Options.HttpsCompression = $true
        } 
        if ($null -ne $DefaultContentType) {
            $Options.DefaultContentType = $DefaultContentType
        }

        $Server.AddStaticFiles($Options)
    }
}
function Add-KrCorsPolicy {
<#
.SYNOPSIS
    Adds a fully‑composed CorsPolicyBuilder (from C# or reflection) and applies it.
#>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline,Mandatory)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder
    )
    process { $Server.AddCors($Name, $Builder) }
}





##########################################################################################

# -------------------------------------------------------------------------
function Add-KrDefaultFile {
    <#
.SYNOPSIS
    Enables automatic index.html (or custom) lookup.
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.KestrunHost]$Server,

        [string]$RequestPath = "",
        [string[]]$DefaultFiles = @("index.html", "index.htm")
    )
    process {
        $Server.AddDefaultFiles({
                param([Microsoft.AspNetCore.Builder.DefaultFilesOptions]$o)
                $o.RequestPath = $RequestPath
                $o.DefaultFileNames.Clear()
                $DefaultFiles | ForEach-Object { $o.DefaultFileNames.Add($_) }
            }) | Out-Null
    }
}

# -------------------------------------------------------------------------
function Add-KrFileServer {
    <#
.SYNOPSIS
    Combines DefaultFiles + StaticFiles (and optional directory browsing).
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory)]
        [string]$PhysicalPath,

        [string]$RequestPath = "",
        [switch]$EnableDirectoryBrowsing
    )
    process {
        $Server.AddFileServer({
                param([Microsoft.AspNetCore.Builder.FileServerOptions]$o)
                $o.RequestPath = $RequestPath
                $o.FileProvider = [Microsoft.Extensions.FileProviders.PhysicalFileProvider]::new($PhysicalPath)
                $o.EnableDirectoryBrowsing = $EnableDirectoryBrowsing.IsPresent
                $o.EnableDefaultFiles = $true
            }) | Out-Null
    }
}

# -------------------------------------------------------------------------
 

# ------------------------------------------------------------------------- 

# -------------------------------------------------------------------------
function Add-KrSignalRHub {
    <#
.SYNOPSIS
    Maps a SignalR hub class to the given URL path.

.EXAMPLE
    $server | Add-KrSignalRHub -HubType ([ChatHub]) -Path "/chat"
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory)]
        [Type]$HubType,

        [Parameter(Mandatory)]
        [string]$Path
    )

    process {
        # 1.  Find the generic method definition on KestrunHost
        $method = $Server.GetType().GetMethods() |
        Where-Object {
            $_.Name -eq 'AddSignalR' -and
            $_.IsGenericMethod -and
            $_.GetParameters().Count -eq 1        # (string path)
        }

        if (-not $method) {
            throw "Could not locate the generic AddSignalR<T>(string) method."
        }

        # 2.  Close the generic with the hub type from the parameter
        $closed = $method.MakeGenericMethod(@($HubType))

        # 3.  Invoke it, passing the path; return the resulting server for chaining
        $closed.Invoke($Server, @($Path)) | Out-Null
    }
}


# ------------------------------------------------------------------------- 