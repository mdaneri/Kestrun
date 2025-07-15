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
        $serializerSettings = [JsonSerializerSettings]::new()
        $serializerSettings.Formatting = if ($Compress) { Formatting.None } else { Formatting.Indented }
        $serializerSettings.ContractResolver = [CamelCasePropertyNamesContractResolver]::new()
        $serializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        $serializerSettings.NullValueHandling = NullValueHandling.Ignore
        $serializerSettings.DefaultValueHandling = DefaultValueHandling.Ignore
        $serializerSettings.MaxDepth = $Depth
        $serializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat
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
        [string]$ContentType
    ) 
         
    try {
        if ($null -ne $Response) {
            # Call the C# method on the $Response object
            $Response.WriteFileResponse((Resolve-Path -Path $FilePath -ErrorAction SilentlyContinue), $Inline.IsPresent, $FileDownloadName, $StatusCode, $ContentType)
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