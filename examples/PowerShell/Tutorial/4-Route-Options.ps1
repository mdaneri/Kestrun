<#
    Sample Kestrun Server Configuration with Multiple Content Types
    This script demonstrates how to set up a simple Kestrun server with multiple routes.
    The server will respond with different content types based on the requested route.
    FileName: 2-Multi-Language-Routes.ps1
#>

# Import the Kestrun module
#Install-PSResource -Name Kestrun


# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

# Enable Kestrun configuration
Enable-KrConfiguration

# Map the route
Add-KrMapRoute -Verbs Get -Pattern "/xml/{message}" -ScriptBlock {
    $message = Get-KrRequestRouteValue -Name 'message'
    Write-KrXmlResponse -InputObject @{ message = $message } -StatusCode 200
}

# YAML Route using MapRouteOption
Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern = "/yaml"
        HttpVerbs = 'Get'
        Code = {
            $message = Get-KrRequestRouteValue -Name 'message'
            Write-KrYamlResponse -InputObject @{ message = $message } -StatusCode 200
        }
        Language = 'PowerShell'
        DisableAntiforgery = $true
    }
)

# JSON Route using MapRouteOption
$options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
$options.Pattern = "/json"
$options.HttpVerbs = [Kestrun.Utilities.HttpVerb[]] @('get')
$options.Code = {
    $message = Get-KrRequestHeader -Name 'message'
    Write-KrJsonResponse -InputObject @{ message = $message } -StatusCode 200
}
$options.Language = 'PowerShell'

Add-KrMapRoute -Options $options

# Start the server asynchronously
Start-KrServer
