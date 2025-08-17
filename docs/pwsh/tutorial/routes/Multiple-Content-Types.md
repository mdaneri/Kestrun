---
title: Multiple Content Types
parent: Tutorials
nav_order: 1
---

# Multiple Content Types

Return text, JSON, XML, and YAML from different routes.

> Prerequisites: see [Introduction](./Introduction.md#prerequisites).

## Full source

File: `examples/PowerShell/Tutorial/2-Multiple-Content-Types.ps1`

```powershell
<#
    Sample Kestrun Server Configuration with Multiple Content Types
    This script demonstrates how to set up a simple Kestrun server with multiple routes.
    The server will respond with different content types based on the requested route.
    FileName: 2-Multiple-Content-Types.ps1
#>

# Import the Kestrun module
Get-Module -Name Kestrun

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

# Map the route for JSON response
Add-KrMapRoute -Verbs Get -Path "/hello-json" -ScriptBlock {
    Write-KrJsonResponse -InputObject @{ message = "Hello, World!" } -StatusCode 200
}

# Map the route for XML response
Add-KrMapRoute -Verbs Get -Path "/hello-xml" -ScriptBlock {
    Write-KrXmlResponse -InputObject @{ message = "Hello, World!" } -StatusCode 200
}

# Map the route for YAML response
Add-KrMapRoute -Verbs Get -Path "/hello-yaml" -ScriptBlock {
    Write-KrYamlResponse -InputObject @{ message = "Hello, World!" } -StatusCode 200
}

# Start the server asynchronously
Start-KrServer
```

## Step-by-step

- The server configuration mirrors Sample 1 (host, listener, runtime, configuration).
- Four routes demonstrate content helpers that set the Content‑Type header and serialize for you:
      - Write‑KrTextResponse — writes text/plain
      - Write‑KrJsonResponse — application/json via JSON serialization
      - Write‑KrXmlResponse — application/xml via XML serialization
      - Write‑KrYamlResponse — application/x‑yaml via YAML serialization
- Each helper also accepts a -StatusCode parameter.

## Try it

### Server

```pwsh
. .\examples\PowerShell\Tutorial\2-Multiple-Content-Types.ps1`
```

### Client

#### curl

```pwsh
curl http://127.0.0.1:5000/hello
curl http://127.0.0.1:5000/hello-json
curl http://127.0.0.1:5000/hello-xml
curl http://127.0.0.1:5000/hello-yaml
```

#### PowerShell

```pwsh
Invoke-WebRequest -Uri 'http://127.0.0.1:5000/hello' | Select-Object -ExpandProperty Content
Invoke-RestMethod -Uri 'http://127.0.0.1:5000/hello-json'             # auto-parses JSON
Invoke-WebRequest -Uri 'http://127.0.0.1:5000/hello-xml'  | Select-Object -ExpandProperty Content
Invoke-WebRequest -Uri 'http://127.0.0.1:5000/hello-yaml' | Select-Object -ExpandProperty Content
```

## Cmdlet references

- [Write-KrTextResponse](docs/pwsh/cmdlets/Write-KrTextResponse)
- [Write-KrJsonResponse](docs/pwsh/cmdlets/Write-KrJsonResponse)
- [Write-KrXmlResponse](docs/pwsh/cmdlets/Write-KrXmlResponse)
- [Write-KrYamlResponse](docs/pwsh/cmdlets/Write-KrYamlResponse)
- [Add-KrMapRoute](docs/pwsh/cmdlets/Add-KrMapRoute)
- [Start-KrServer](docs/pwsh/cmdlets/Start-KrServer)

## Troubleshooting

- JSON not parsed by Invoke‑WebRequest: prefer Invoke‑RestMethod for JSON content.
- YAML shown as raw text: that’s expected; PowerShell doesn’t auto‑parse YAML by default.
