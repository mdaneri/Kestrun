<#
.SYNOPSIS
    Writes an object to the HTTP response body as JSON.

.DESCRIPTION
    Serializes the provided object to JSON using Newtonsoft.Json and writes it
    to the current HTTP response. The caller can specify the HTTP status code,
    serialization depth and formatting options.
#>
function Write-KrJsonResponse {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [ValidateRange(0, 100)]
        [int]$Depth = 10,
        [Parameter()]
        [bool]$Compress = $false,
        [Parameter()]
        [string]$ContentType
    )
    if ($null -ne $Response) {
        $serializerSettings =  [Newtonsoft.Json.JsonSerializerSettings]::new()
        $serializerSettings.Formatting = if ($Compress) { [Newtonsoft.Json.Formatting]::None } else { [Newtonsoft.Json.Formatting]::Indented }
        $serializerSettings.ContractResolver = [Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver]::new()
        $serializerSettings.ReferenceLoopHandling = [Newtonsoft.Json.ReferenceLoopHandling]::Ignore
        $serializerSettings.NullValueHandling = [Newtonsoft.Json.NullValueHandling]::Ignore
        $serializerSettings.DefaultValueHandling = [Newtonsoft.Json.DefaultValueHandling]::Ignore
        $serializerSettings.MaxDepth = $Depth
        $serializerSettings.DateFormatHandling = [Newtonsoft.Json.DateFormatHandling]::IsoDateFormat
        # Call the C# method on the $Response object
        $Response.WriteJsonResponse($InputObject, $serializerSettings, $ContentType)
    }
}


<#
.SYNOPSIS
    Writes an object to the HTTP response body as YAML.

.DESCRIPTION
    Serializes the provided object to YAML using the underlying C# helper and
    sets the specified status code on the response.
#>
function Write-KrYamlResponse {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteYamlResponse($InputObject, $StatusCode, $ContentType)
    }
}

<#
.SYNOPSIS
    Writes plain text to the HTTP response body.

.DESCRIPTION
    Sends a raw text payload to the client and optionally sets the HTTP status
    code and content type.
#>
function Write-KrTextResponse {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteTextResponse($InputObject, $StatusCode, $ContentType)
    }
}

<#
.SYNOPSIS
    Writes an object serialized as XML to the HTTP response.

.DESCRIPTION
    Converts the provided object to XML and writes it to the response body. The
    status code and content type can be customized.
#>
function Write-KrXmlResponse {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteXmlResponse($InputObject, $StatusCode, $ContentType)
    }
}

<#
.SYNOPSIS
    Sends a file as the HTTP response.

.DESCRIPTION
    Writes a file from disk to the response body. The file path is resolved
    relative to the Kestrun root if required. Additional options allow
    specifying the download name, forcing inline display and custom content
    type.
#>
function Write-KrFileResponse {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$FileDownloadName,
        [Parameter()]
        [switch]$Inline,
        [Parameter()]
        [string]$ContentType 
    )

    try {
        if ($null -ne $Response) {
            $resolvedPath = Resolve-KrPath -Path $FilePath -KestrunRoot -Test
            Write-KrLog -level "Verbose" -Message "Resolved file path: $resolvedPath"
            # Call the C# method on the $Response object
            $Response.WriteFileResponse($resolvedPath, $ContentType, $StatusCode)
            Write-Information "File response written for $FilePath with download name $FileDownloadName"
        }
    }
    catch {
        Write-Error "Error writing file response: $_"
    }
}
<#
.SYNOPSIS
    Issues an HTTP redirect response to the client.

.DESCRIPTION
    Sets the Location header to the provided URL and optionally includes a
    message body describing the redirect.
#>
function Write-KrRedirectResponse {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [Parameter()]
        [string]$Message
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteRedirectResponse($Url, $Message)
    }
}
 

<#
.SYNOPSIS
    Writes binary data directly to the HTTP response body.

.DESCRIPTION
    Sends a byte array to the client. Useful for returning images or other
    binary content with a specified status code and content type.
#>
function Write-KrBinaryResponse {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteBinaryResponse($InputObject, $StatusCode, $ContentType)
    }
}

<#
.SYNOPSIS
    Writes a stream to the HTTP response body.

.DESCRIPTION
    Copies the provided stream to the response output stream. Useful for
    forwarding large files or custom streaming scenarios.
#>
function Write-KrStreamResponse {
    param(
        [Parameter(Mandatory = $true)]
        [stream]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteStreamResponse($InputObject, $StatusCode, $ContentType)
    }
}