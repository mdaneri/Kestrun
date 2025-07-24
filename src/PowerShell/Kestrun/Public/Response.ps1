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
        $serializerSettings = [Newtonsoft.Json.JsonSerializerSettings]::new()
        $serializerSettings.Formatting = if ($Compress) { [Newtonsoft.Json.Formatting]::None } else { [Newtonsoft.Json.Formatting]::Indented }
        $serializerSettings.ContractResolver = [Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver]::new()
        $serializerSettings.ReferenceLoopHandling = [Newtonsoft.Json.ReferenceLoopHandling]::Ignore
        $serializerSettings.NullValueHandling = [Newtonsoft.Json.NullValueHandling]::Ignore
        $serializerSettings.DefaultValueHandling = [Newtonsoft.Json.DefaultValueHandling]::Ignore
        $serializerSettings.MaxDepth = $Depth
        $serializerSettings.DateFormatHandling = [Newtonsoft.Json.DateFormatHandling]::IsoDateFormat
        # Call the C# method on the $Response object
        $Response.WriteJsonResponse($InputObject, $serializerSettings, $StatusCode, $ContentType)
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


function Write-KrBsonResponse {
    <#
    .SYNOPSIS
        Writes an object serialized as BSON to the HTTP response.
    .DESCRIPTION
        Converts the provided object to BSON format and writes it to the response body. The status code and content type can be customized.
    .PARAMETER InputObject  
        The object to serialize and write to the response.              
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200.
    .PARAMETER ContentType
        The content type to set for the response. If not specified, defaults to application/bson
    .EXAMPLE
        Write-KrBsonResponse -InputObject $myObject -StatusCode 200 -ContentType "application/bson"
        Writes the $myObject serialized as BSON to the response with a 200 status code and      
        content type "application/bson".
    #>  
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
        $Response.WriteBsonResponse($InputObject, $StatusCode, $ContentType)
    }
}

function Write-KrCborResponse {
    <#
    .SYNOPSIS
        Writes an object serialized as CBOR to the HTTP response.
    .DESCRIPTION
        Converts the provided object to CBOR format and writes it to the response body. The status code and content type can be customized.
    .PARAMETER InputObject
        The object to serialize and write to the response.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200.
    .PARAMETER ContentType
        The content type to set for the response. If not specified, defaults to application/cbor
    .EXAMPLE
        Write-KrCborResponse -InputObject $myObject -StatusCode 200 -ContentType "application/cbor"
        Writes the $myObject serialized as CBOR to the response with a 200 status code and
        content type "application/cbor".
    #>
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
        $Response.WriteCborResponse($InputObject, $StatusCode, $ContentType)
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
        [string]$ContentType,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$FileDownloadName,
        [Parameter()]
        [Kestrun.ContentDispositionType]$ContentDisposition = [Kestrun.ContentDispositionType]::NoContentDisposition
    )

    try {
        if ($null -ne $Response) {
            $resolvedPath = Resolve-KrPath -Path $FilePath -KestrunRoot -Test
            Write-KrVerboseLog -MessageTemplate "Resolved file path: $resolvedPath"
            if ($ContentDisposition -ne [Kestrun.ContentDispositionType]::NoContentDisposition) {
                $Response.ContentDisposition.Type = $ContentDisposition.ToString()
            }

            if (!([string]::IsNullOrEmpty($FileDownloadName))) {
                $Response.ContentDisposition.FileName = $FileDownloadName
            }

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

function Write-KrResponse {
    param(
        [Parameter(Mandatory = $true)]
        [stream]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteResponse($InputObject, $StatusCode, $ContentType)
    }
}

function Write-KrErrorResponse {
    [CmdletBinding(DefaultParameterSetName = 'Message')]
    param (
        [Parameter(ParameterSetName = 'Message', Mandatory = $true)]
        [string]$Message,

        [Parameter(ParameterSetName = 'Exception', Mandatory = $true)]
        [System.Exception]$Exception,

        [Parameter()]
        [int]$StatusCode = 500,

        [Parameter()]
        [string]$ContentType,

        [Parameter()]
        [string]$Details,

        [Parameter()]
        [switch]$IncludeStack 
    )

    if ($PSCmdlet.ParameterSetName -eq "Message") {
        $Response.WriteErrorResponse(
            $Message,
            $StatusCode,
            $ContentType,
            $Details
        )
    }
    else {
        $Response.WriteErrorResponse(
            $Exception,
            $StatusCode,
            $ContentType,
            $IncludeStack.IsPresent
        )
    }
}
