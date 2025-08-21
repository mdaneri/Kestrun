<#
    .SYNOPSIS
        Retrieves a request body value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the request body.
    .EXAMPLE
        $value = Get-KrRequestBody
        Retrieves the value of the request body from the HTTP request.
    .OUTPUTS
        Returns the value of the request body, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestBody {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param()
    if ($null -ne $Context.Request) {
        # Get the request body value from the request
        return $Context.Request.Body
    }
}
