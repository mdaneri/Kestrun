---
title: Tutorials
parent: PowerShell

nav_order: 0 
---

# Tutorials

Step-by-step guides to build and ship with Kestrun.

## Prerequisites

- PowerShell 7.4, 7.5, or 7.6
- .NET Runtime (SDK not required)
  - With PowerShell 7.4 or 7.5: install .NET 8 Runtime AND ASP.NET Core Runtime
  - With PowerShell 7.6: install .NET 9 Runtime AND ASP.NET Core Runtime
- Kestrun module: installed or available from this repository at `src/PowerShell/Kestrun/Kestrun.psm1`
- Supported OS: same as .NET 8/9 (Windows, Linux, macOS), including ARM/ARM64

## Quick start: run the samples

From the repository root:

```powershell
# 1) Hello World
pwsh .\examples\PowerShell\Tutorial\1-Hello-World.ps1
```

Then browse the routes (default listener: <http://127.0.0.1:5000>):
Read the note on each sample for the routes detail.

Stop the server with Ctrl+C in the terminal.

## What each sample shows

### 1-Hello-World: Minimal server and one route

File: [`examples/PowerShell/Tutorial/1-Hello-World.ps1`][1-Hello-World.ps1]

- Create a server, add a listener on 127.0.0.1:5000
- Enable the PowerShell runtime and configuration
- Map a GET route and return a text response

### 2-Multiple-Content-Types: Content negotiation made simple

File: [`examples/PowerShell/Tutorial/2-Multiple-Content-Types.ps1`][2-Multiple-Content-Types.ps1]

- Return JSON, XML, YAML, and plain text using dedicated helpers
- See how to call `Write-KrJsonResponse`, `Write-KrXmlResponse`, `Write-KrYamlResponse`, and `Write-KrTextResponse`

### 3-Multi-Language-Routes: Mix languages inline

File: [`examples/PowerShell/Tutorial/3-Multi-Language-Routes.ps1`][3-Multi-Language-Routes.ps1]

- Keep your server and plumbing in PowerShell
- Author individual routes in C# or VB.NET using the `-Language` and `-Code` parameters

### 4-Route-Options: Various way to configure a route

File: [`examples/PowerShell/Tutorial/4-Route-Options.ps1`][4-Route-Options.ps1]

- Demonstrates simple parameters vs MapRouteOptions via hashtable or strongly typed object
- Mix PowerShell and C# handlers, use headers, route params, query, content helpers
- Toggle per‑route features (DisableAntiforgery) and set language explicitly

[1-Hello-World.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/1-Hello-World.ps1
[2-Multiple-Content-Types.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/2-Multiple-Content-Types.ps1
[3-Multi-Language-Routes.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/3-Multi-Language-Routes.ps1
[4-Route-Options.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/4-Route-Options.ps1
