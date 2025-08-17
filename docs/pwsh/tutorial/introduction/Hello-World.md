---
title: Hello World
parent: Introduction
nav_order: 1
---

# Hello World — Minimal Server and One Route

This sample spins up a small Kestrun server and returns plain text from a single route.

> Prerequisites: see [Introduction](./Introduction.md#prerequisites).

## Full source

File: `examples/PowerShell/Tutorial/1-Hello-World.ps1`

```powershell
<#
    Sample Kestrun Server Configuration
    This script demonstrates how to set up a simple Kestrun server with a single route.
    The server will respond with "Hello, World!" when accessed.
    FileName: 1-Hello-World.ps1
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
    # Or the shorter version
    # Write-KrTextResponse "Hello, World!"
}

# Start the server asynchronously
Start-KrServer
```

## Step-by-step

- New-KrServer — creates an in‑process server host that will hold listeners, middleware, and routes.
- Add-KrListener — binds the Kestrel web server to 127.0.0.1:5000 over HTTP/1.1 (default).
- Add-KrPowerShellRuntime — registers the PowerShell runtime so routes and middleware implemented in PS can execute.
- Enable-KrConfiguration — applies your staged configuration (listeners, runtimes, options) to the running host.
- Add-KrMapRoute — maps a GET /hello route with a PowerShell script block handler.
- Write-KrTextResponse — sets the HTTP status code (200) and writes a text/plain response body.
- Start-KrServer — starts listening; press Ctrl+C in the terminal to stop.

### How it works

- Configuration is staged by cmdlets, then committed with Enable-KrConfiguration.
- Each route runs inside a request context (available as $Context) with Request/Response properties.
- Write‑KrTextResponse is a convenience wrapper that sets Content‑Type to text/plain and writes the body.
- By default, a single listener is configured on 127.0.0.1:5000; you can add more listeners (e.g., HTTPS) later.

## Try it

### Server

```pwsh
. .\examples\PowerShell\Tutorial\1-Hello-World.ps1`
```

### Client

#### curl

```pwsh
curl http://127.0.0.1:5000/hello
```

#### PowerShell

```pwsh
Invoke-WebRequest -Uri 'http://127.0.0.1:5000/hello' | Select-Object -ExpandProperty Content

# Stop the server with Ctrl+C in the terminal where you ran the script.
```

## Cmdlet references

- [New-KrServer](/docs/pwsh/cmdlets/New-KrServer)
- [Add-KrListener](/docs/pwsh/cmdlets/Add-KrListener)
- [Add-KrPowerShellRuntime](/docs/pwsh/cmdlets/Add-KrPowerShellRuntime)
- [Enable-KrConfiguration](/docs/pwsh/cmdlets/Enable-KrConfiguration)
- [Add-KrMapRoute](/docs/pwsh/cmdlets/Add-KrMapRoute)
- [Write-KrTextResponse](/docs/pwsh/cmdlets/Write-KrTextResponse)
- [Start-KrServer](/docs/pwsh/cmdlets/Start-KrServer)

## Troubleshooting

- If the port is in use, choose a different port in Add‑KrListener.
- If responses are empty, ensure Add‑KrPowerShellRuntime was called before Enable‑KrConfiguration.
