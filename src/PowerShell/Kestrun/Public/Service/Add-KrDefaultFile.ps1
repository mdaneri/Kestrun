
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
    [KestrunRuntimeApi([KestrunApiContext]::Definition)]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Builder.DefaultFilesOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RequestPath,

        [Parameter(ParameterSetName = 'Items')]
        [string[]]$DefaultFiles,

        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.Extensions.FileProviders.IFileProvider] $FileProvider,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$RedirectToAppendTrailingSlash,

        [Parameter()]
        [switch]$PassThru
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
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        
        # Add the default file provider to the server
        [Kestrun.Hosting.KestrunHostStaticFilesExtensions]::AddDefaultFiles($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}