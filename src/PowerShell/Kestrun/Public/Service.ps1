 
# -------------------------------------------------------------------------
function Add-KrPowerShellRazorPagesRuntime {
    <#
.SYNOPSIS
   Adds PowerShell support for Razor Pages.
.DESCRIPTION
    This cmdlet allows you to register Razor Pages with PowerShell support in the Kestrun server.
    It can be used to serve dynamic web pages using Razor syntax with PowerShell code blocks.
.PARAMETER Server
    The Kestrun server instance to which the PowerShell Razor Pages service will be added.
.PARAMETER PathPrefix
    An optional path prefix for the Razor Pages. If specified, the Razor Pages will be served under this path.
.EXAMPLE
    $server | Add-KrPowerShellRazorPagesRuntime -PathPrefix '/pages'
    This example adds PowerShell support for Razor Pages to the server, with a path prefix of '/pages'.
.EXAMPLE
    $server | Add-KrPowerShellRazorPagesRuntime
    This example adds PowerShell support for Razor Pages to the server without a path prefix.
.NOTES
    This cmdlet is used to register Razor Pages with PowerShell support in the Kestrun server, allowing you to serve dynamic web pages using Razor syntax with PowerShell code blocks.
#>
    [CmdletBinding()]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [string]$PathPrefix
    )
    process {
        if ([string]::IsNullOrWhiteSpace($PathPrefix)) {
            $Server.AddPowerShellRazorPages() | Out-Null
        }
        else {
            $Server.AddPowerShellRazorPages([Microsoft.AspNetCore.Http.PathString]::new($PathPrefix)) | Out-Null
        } 
        # Return the modified server instance
        return $Server
    }
}
function Add-KrRazorPageService {
    <#
.SYNOPSIS
    Adds Razor Pages service to the server.
.DESCRIPTION
    This cmdlet allows you to register Razor Pages with the Kestrun server.
    It can be used to serve dynamic web pages using Razor syntax.
.PARAMETER Server
    The Kestrun server instance to which the Razor Pages service will be added.
.PARAMETER Options
    The RazorPagesOptions to configure the Razor Pages service.
.PARAMETER RootDirectory
    The root directory for the Razor Pages.
.PARAMETER Conventions
    An array of page conventions to apply to the Razor Pages.
.EXAMPLE
    $server | Add-KrRazorPageService -RootDirectory '/Pages' -Conventions $conventions
    This example adds Razor Pages service to the server, specifying the root directory and conventions for the pages.
.EXAMPLE
    $server | Add-KrRazorPageService -Options $options
    This example adds Razor Pages service to the server using the specified RazorPagesOptions.
.LINK
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.razorpages.razorpagesoptions?view=aspnetcore-8.0
.NOTES
    This cmdlet is used to register Razor Pages with the Kestrun server, allowing you to serve dynamic web pages using Razor syntax.
#>
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RootDirectory,

        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.AspNetCore.Mvc.ApplicationModels.IPageConventions[]]$Conventions = @()
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions]::new()
            if (-not [string]::IsNullOrWhiteSpace($RootDirectory)) {
                $Options.RootDirectory = $RootDirectory
            }
            if ($Conventions.Count -gt 0) {
                foreach ($c in $Conventions) {
                    $Options.Conventions.Add($c)
                }
            }
        }
        # Add Razor Pages service to the server
        $Server.AddRazorPages($Options) | Out-Null
        # Return the modified server instance
        return $Server
    }
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
    Adds response compression to the server.
.DESCRIPTION
    This cmdlet allows you to configure response compression for the Kestrun server.
    It can be used to compress responses using various algorithms like Gzip, Brotli, etc.
.PARAMETER Server
    The Kestrun server instance to which the response compression will be added.
.PARAMETER Options
    The ResponseCompressionOptions to configure the response compression.
.PARAMETER EnableForHttps
    If specified, enables response compression for HTTPS requests.
.PARAMETER MimeTypes
    An array of MIME types to compress. If not specified, defaults to common text-based MIME types.
.PARAMETER ExcludedMimeTypes
    An array of MIME types to exclude from compression.
 
.EXAMPLE
    $server | Add-KrResponseCompression -EnableForHttps -MimeTypes 'text/plain', 'application/json' -ExcludedMimeTypes 'image/*' -Providers $gzipProvider, $brotliProvider
    This example adds response compression to the server, enabling it for HTTPS requests, and specifying the MIME types to compress and exclude, as well as the compression providers to use.
.EXAMPLE
    $server | Add-KrResponseCompression -Options $options
    This example adds response compression to the server using the specified ResponseCompressionOptions.
.LINK
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.responsecompression.responsecompressionoptions?view=aspnetcore-8.0

.NOTES
    This cmdlet is used to configure response compression for the Kestrun server, allowing you to specify which MIME types should be compressed and which should be excluded.
    Providers is not supported yet.
#>
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.ResponseCompression.ResponseCompressionOptions]$Options,
        [Parameter(ParameterSetName = 'Items')]
        [switch]$EnableForHttps,
        [Parameter(ParameterSetName = 'Items')]
        [string[]]$MimeTypes,
        [Parameter(ParameterSetName = 'Items')]
        [string[]]$ExcludedMimeTypes
        # Uncomment when Providers are supported
        # [Parameter(ParameterSetName = 'Items')]
        # [Microsoft.AspNetCore.ResponseCompression.ICompressionProvider[]]$Providers = @()
    )
    process {
         # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.ResponseCompression.ResponseCompressionOptions]::new()
            if ($null -ne $MimeTypes -and $MimeTypes.Count -gt 0) {
                $Options.MimeTypes = $MimeTypes
            }
            if ($null -ne $ExcludedMimeTypes -and $ExcludedMimeTypes.Count -gt 0) {
                $Options.ExcludedMimeTypes = $ExcludedMimeTypes
            }
            if ($EnableForHttps.IsPresent) {
                $Options.EnableForHttps = $true
            }
            # Providers are not supported yet
            <# if ($null -ne $Providers -and $Providers.Count -gt 0) {
                foreach ($Provider in $Providers) {
                    $Options.Providers.Add($Provider)
                }
            }#>
        }

        $Server.AddResponseCompression($Options) | Out-Null
        # Return the modified server instance
        return $Server
    }
}

function Add-KrStaticFilesService {
    <#
.SYNOPSIS
    Registers a static file server to serve files from a specified path.
.DESCRIPTION
    This cmdlet allows you to serve static files from a specified path using the Kestrun server.
    It can be used to serve files like images, stylesheets, and scripts.
.PARAMETER Server
    The Kestrun server instance to which the static file service will be added.
.PARAMETER Options
    The StaticFileOptions to configure the static file service.
.PARAMETER FileProvider
    An optional file provider to use for serving the files. 
.PARAMETER RequestPath
    The path at which the static file service will be registered.
.PARAMETER HttpsCompression
    If specified, enables HTTPS compression for the static files.
.PARAMETER ServeUnknownFileTypes
    If specified, allows serving files with unknown MIME types.
.PARAMETER DefaultContentType
    The default content type to use for files served by the static file service.
.PARAMETER RedirectToAppendTrailingSlash
    If specified, redirects requests to append a trailing slash to the URL.
.EXAMPLE
    $server | Add-KrStaticFilesService -RequestPath '/static' -HttpsCompression -ServeUnknownFileTypes -DefaultContentType 'application/octet-stream' -RedirectToAppendTrailingSlash
    This example adds a static file service to the server for the path '/static', enabling HTTPS compression, allowing serving unknown file types, setting the default content type to 'application/octet-stream', and redirecting requests to append a trailing slash.
.EXAMPLE
    $server | Add-KrStaticFilesService -Options $options
    This example adds a static file service to the server using the specified StaticFileOptions.
.LINK
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.staticfileoptions?view=aspnetcore-8.0
.NOTES
    ContentTypeProvider and ContentTypeProviderOptions are not supported yet.
#>
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Builder.StaticFileOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.Extensions.FileProviders.PhysicalFileProvider]$FileProvider,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RequestPath,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$HttpsCompression,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$ServeUnknownFileTypes,

        [Parameter(ParameterSetName = 'Items')]
        [string]$DefaultContentType,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$RedirectToAppendTrailingSlash
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Builder.StaticFileOptions]::new()
            if ($null -ne $FileProvider) {
                $Options.FileProvider = $FileProvider
            }
            if (-not [string]::IsNullOrEmpty($RequestPath)) {
                $Options.RequestPath = [Microsoft.AspNetCore.Http.PathString]::new($RequestPath)
            }
            if ($ServeUnknownFileTypes.IsPresent) {
                $Options.ServeUnknownFileTypes = $true
            }
            if ($HttpsCompression.IsPresent) {
                $Options.HttpsCompression = $true
            }
            if (-not [string]::IsNullOrEmpty($DefaultContentType)) {
                $Options.DefaultContentType = $DefaultContentType
            }
            if ($RedirectToAppendTrailingSlash.IsPresent) {
                $Options.RedirectToAppendTrailingSlash = $true
            }
        }

        $Server.AddStaticFiles($Options) | Out-Null
        # Return the modified server instance
        return $Server
    }
}

function Add-KrCorsPolicy {
    <#
.SYNOPSIS
    Adds a CORS policy to the server.
.DESCRIPTION
    This cmdlet allows you to configure a CORS policy for the Kestrun server.
    It can be used to specify allowed origins, methods, headers, and other CORS settings.
.PARAMETER Server
    The Kestrun server instance to which the CORS policy will be added.
.PARAMETER Name
    The name of the CORS policy.
.PARAMETER Builder
    The CORS policy builder to configure the CORS policy.

.PARAMETER AllowAnyOrigin
    If specified, allows any origin to access the resources.
.PARAMETER AllowAnyMethod
    If specified, allows any HTTP method to be used in requests.
.PARAMETER AllowAnyHeader
    If specified, allows any header to be included in requests.
    If not specified, only headers explicitly allowed will be included. 
.PARAMETER AllowCredentials
    If specified, allows credentials (cookies, authorization headers, etc.) to be included in requests.
.PARAMETER DisallowCredentials
    If specified, disallows credentials in requests.
    If not specified, credentials will be allowed.

.EXAMPLE
    $server | Add-KrCorsPolicy -Name 'AllowAll' -AllowAnyOrigin -AllowAnyMethod -AllowAnyHeader
    This example adds a CORS policy named 'AllowAll' to the server, allowing any origin, method, and header.
.EXAMPLE
    $server | Add-KrCorsPolicy -Name 'CustomPolicy' -Builder $builder
    This example adds a CORS policy named 'CustomPolicy' to the server using the specified CORS policy builder.
.EXAMPLE
    $server | Add-KrCorsPolicy -Server $server -Name 'CustomPolicy' -AllowAnyOrigin -AllowAnyMethod -AllowAnyHeader
    This example adds a CORS policy named 'CustomPolicy' to the server, allowing any origin, method, and header.
.NOTES
    This cmdlet is used to configure CORS policies for the Kestrun server, allowing you to control cross-origin requests and specify which origins, methods, and headers are allowed.
 .LINK
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.cors.infrastructure.corspolicybuilder?view=aspnetcore-8.0
#>
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$AllowAnyOrigin,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$AllowAnyMethod,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$AllowAnyHeader,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$AllowCredentials,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$DisallowCredentials
    ) 
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {

            if ($AllowCredentials.IsPresent -and $DisallowCredentials.IsPresent) {
                throw "Cannot specify both AllowCredentials and DisallowCredentials."
            }

            $Builder = [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]::new()
            if ($AllowAnyOrigin.IsPresent) {
                $Builder.AllowAnyOrigin() | Out-Null
            }
            if ($AllowAnyMethod.IsPresent) {
                $Builder.AllowAnyMethod() | Out-Null
            }
            if ($AllowAnyHeader.IsPresent) {
                $Builder.AllowAnyHeader() | Out-Null
            }
            if ($AllowCredentials.IsPresent) {
                $Builder.AllowCredentials() | Out-Null
            }
            if ($DisallowCredentials.IsPresent) {
                $Builder.DisallowCredentials() | Out-Null
            }
        }
        $Server.AddCors($Name, $Builder) | Out-Null
        # Return the modified server instance
        return $Server
    }
}

function Add-KrAntiforgery {
    <#
.SYNOPSIS
    Adds an Antiforgery service to the server.
.DESCRIPTION
    This cmdlet allows you to configure the Antiforgery service for the Kestrun server.
    It can be used to protect against Cross-Site Request Forgery (CSRF) attacks by generating and validating antiforgery tokens.
.PARAMETER Server
    The Kestrun server instance to which the Antiforgery service will be added.
.PARAMETER Options
    The Antiforgery options to configure the service.
.PARAMETER Cookie
    The cookie builder to use for the Antiforgery service.
.PARAMETER FormFieldName
    The name of the form field to use for the Antiforgery token.
.PARAMETER HeaderName
    The name of the header to use for the Antiforgery token.
.PARAMETER SuppressXFrameOptionsHeader
    If specified, the X-Frame-Options header will not be added to responses.
.EXAMPLE
    $server | Add-KrAntiforgery -Cookie $cookieBuilder -FormField '__RequestVerificationToken' -HeaderName 'X-CSRF-Token' -SuppressXFrameOptionsHeader
    This example adds an Antiforgery service to the server with a custom cookie builder, form field name, and header name.
.EXAMPLE
    $server | Add-KrAntiforgery -Options $options
    This example adds an Antiforgery service to the server using the specified Antiforgery options.
.LINK
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.antiforgery.antiforgeryoptions?view=aspnetcore-8.0
 #>
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [Kestrun.CookieBuilder]$Cookie = $null,

        [Parameter(ParameterSetName = 'Items')]
        [string]$FormFieldName,

        [Parameter(ParameterSetName = 'Items')]
        [string]$HeaderName,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$SuppressXFrameOptionsHeader
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions]::new()
            if ($null -ne $Cookie) {
                $Options.Cookie = $Cookie
            }
            if (-not [string]::IsNullOrEmpty($FormFieldName)) {
                $Options.FormFieldName = $FormFieldName
            }
            if (-not [string]::IsNullOrEmpty($HeaderName)) {
                $Options.HeaderName = $HeaderName
            }
            if ($SuppressXFrameOptionsHeader.IsPresent) {
                $Options.SuppressXFrameOptionsHeader = $true
            }
        } 
        $Server.AddAntiforgery($Options) | Out-Null
        # Return the modified server instance
        return $Server
    }
}
 
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

function Add-KrFileServer {
    <#
.SYNOPSIS
    Registers a file server to serve static files from a specified path.
.DESCRIPTION
    This cmdlet allows you to serve static files from a specified path using the Kestrun server.
    It can be used to serve files like images, stylesheets, and scripts.
.PARAMETER Server
    The Kestrun server instance to which the file server will be added.
.PARAMETER Options  
    The FileServerOptions to configure the file server.
.PARAMETER FileProvider 
    An optional file provider to use for serving the files.
.PARAMETER RequestPath
    The path at which the file server will be registered.
.PARAMETER EnableDirectoryBrowsing
    If specified, enables directory browsing for the file server.
.PARAMETER RedirectToAppendTrailingSlash
    If specified, requests to the path will be redirected to append a trailing slash.

.EXAMPLE
    $server | Add-KrFileServer -RequestPath '/files' -EnableDirectoryBrowsing
    This example adds a file server to the server for the path '/files', enabling directory browsing.
    The file server will use the default options for serving static files.
.EXAMPLE
    $server | Add-KrFileServer -Options $options
    This example adds a file server to the server using the specified FileServerOptions.
.LINK
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.fileserveroptions?view=aspnetcore-8.0

#>
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Builder.FileServerOptions]$Options,


        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.Extensions.FileProviders.PhysicalFileProvider]$FileProvider,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RequestPath,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$EnableDirectoryBrowsing,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$RedirectToAppendTrailingSlash
 
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Builder.FileServerOptions]::new()

            if (-not [string]::IsNullOrEmpty($RequestPath)) {
                $Options.RequestPath = [Microsoft.AspNetCore.Http.PathString]::new($RequestPath)
            }
            if ($null -ne $FileProvider) {
                $Options.FileProvider = $FileProvider
            }
            if ($EnableDirectoryBrowsing.IsPresent) {
                $Options.EnableDirectoryBrowsing = $true
            }
            if ($RedirectToAppendTrailingSlash.IsPresent) {
                $Options.RedirectToAppendTrailingSlash = $true
            }
        }
        $Server.AddFileServer($Options) | Out-Null

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