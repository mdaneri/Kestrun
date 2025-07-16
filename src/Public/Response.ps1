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
        [string]$ContentType,
        [Parameter()]
        [switch]$EmbedFileContent
    )

    try {
        if ($null -ne $Response) {
            $resolvedPath = Resolve-KrPath -Path $FilePath -KestrunRoot -Test
            Write-KrLog -level "Verbose" -Message "Resolved file path: $resolvedPath"
            # Call the C# method on the $Response object
            $Response.WriteFileResponse($resolvedPath, $Inline.IsPresent, $FileDownloadName, $StatusCode, $ContentType, $EmbedFileContent.IsPresent)
            Write-Information "File response written for $FilePath with download name $FileDownloadName"
        }
    }
    catch {
        Write-Error "Error writing file response: $_"
    }
}
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