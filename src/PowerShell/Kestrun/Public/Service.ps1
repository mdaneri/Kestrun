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

# ----------- 

 

 
 
 
function Add-KrDefaultFile {
    <#
.SYNOPSIS
    Registers a default file provider for static files (e.g. index.html).
.DESCRIPTION
    This cmdlet allows you to specify a default file that should be served
    when a request is made to a directory without a specific file name.
    It can be used to serve files like index.html or default.aspx.
.PARAMETER Server
    The Kestrun server instance to which the default file provider will be added.   
.PARAMETER Options
    The DefaultFilesOptions to configure the default file provider. 
.PARAMETER RequestPath
    The path at which the default file provider will be registered. 
.PARAMETER DefaultFiles
    An array of default file names to be served when a request is made to the specified path.
.PARAMETER FileProvider
    An optional file provider to use for serving the default files.
.PARAMETER RedirectToAppendTrailingSlash
    If specified, requests to the path will be redirected to append a trailing slash.

.EXAMPLE
    $server | Add-KrDefaultFile -RequestPath '/static' -DefaultFiles 'index.html', 'default.aspx' -RedirectToAppendTrailingSlash
    This example adds a default file provider to the server for the path '/static',
    serving the files 'index.html' and 'default.aspx' when a request is made to that path.

.EXAMPLE
    $server | Add-KrDefaultFile -Options $options
    This example adds a default file provider to the server using the specified DefaultFilesOptions.
.LINK
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.defaultfilesoptions?view=aspnetcore-8.0
#>
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Builder.DefaultFilesOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RequestPath,
        [Parameter(ParameterSetName = 'Items')]
        [string[]]$DefaultFiles,
        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.Extensions.FileProviders.IFileProvider] $FileProvider,
        [Parameter(ParameterSetName = 'Items')]
        [switch]$RedirectToAppendTrailingSlash
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Builder.DefaultFilesOptions]::new()
            if ($RedirectToAppendTrailingSlash.IsPresent) {
                $Options.RedirectToAppendTrailingSlash = $true
            }

            if ($null -ne $FileProvider) {
                $Options.FileProvider = $FileProvider
            }

            foreach ($file in $DefaultFiles) {
                $Options.DefaultFileNames.Add($file)
            }
            if (-not [string]::IsNullOrEmpty($RequestPath)) {
                $Options.RequestPath = [Microsoft.AspNetCore.Http.PathString]::new($RequestPath)
            }
        }
        $Server.AddDefaultFiles($Options) | Out-Null
        # Return the modified server instance
        return $Server
    }
}
 

##########################################################################################

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