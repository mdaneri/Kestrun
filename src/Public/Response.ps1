function Write-KrJsonResponse {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [ValidateRange(0, 100)]
        [int]$Depth = 10
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteJsonResponse(  $InputObject, $Depth, $StatusCode)
    }
}


function Write-KrYamlResponse {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [ValidateRange(0, 100)]
        [int]$Depth = 10
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteYamlResponse(  $InputObject, $Depth, $StatusCode)
    }
}

function Write-KrTextResponse {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200 
    )
    if ($null -ne $Response) {
        # Call the C# method on the $Response object
        $Response.WriteTextResponse(  $InputObject, $StatusCode)
    }
}