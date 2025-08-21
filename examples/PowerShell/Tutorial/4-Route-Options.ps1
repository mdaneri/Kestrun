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
Add-KrMapRoute -Verbs Get -Path "/hello" -ScriptBlock {
    Write-KrTextResponse -InputObject "Hello, World!" -StatusCode 200
}

Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern = "/yaml"
        HttpVerbs = 'Get'
        Code = {
            $message = $Context.Request.Query['message']
            Write-KrYamlResponse -InputObject @{ message = $message } -StatusCode 200
        }
        Language = 'PowerShell'
        # DisableAntiforgery = $true
    }
)

$options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
$options.Pattern = "/json"
$options.HttpVerbs = [Kestrun.Utilities.HttpVerb[]] @('get')
$options.Code = {
    $message = $Context.Request.Headers['message']
    Write-KrJsonResponse -InputObject @{ message = $message } -StatusCode 200
}
$options.Language = 'PowerShell'

Add-KrMapRoute -Options $options

# Start the server asynchronously
Start-KrServer
