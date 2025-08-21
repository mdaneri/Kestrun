---
title: Introduction
parent: Tutorials
nav_order: 1
---

# Introduction to Kestrun (PowerShell)

Kestrun is a lightweight, cross‑platform web server you can configure entirely from PowerShell
(and optionally mix with C# or VB.NET inline). It sits on ASP.NET Core Kestrel, so you get
performance, HTTP/1.1–HTTP/3 support, TLS, compression, and familiar middleware patterns — with
simple PowerShell cmdlets.

This chapter gives you a hands‑on start using three tiny samples you can run immediately:

- Sample-1: Hello World route returning plain text
- Sample-2: Multiple content types (text, JSON, XML, YAML)
- Sample-3: Multi-language routes (PowerShell, C#, VB.NET)

All sample scripts live in `examples/PowerShell/Tutorial`.

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

- 1-Hello-World.ps1: GET /hello
- 2-Multiple-Content-Types: GET /hello, /hello-json, /hello-xml, /hello-yaml
- 3-Multi-Language-Routes: GET /hello (defined in PowerShell, C#, and VB.NET examples)

Stop the server with Ctrl+C in the terminal.

## What each sample shows

### 1-Hello-World: Minimal server and one route

- Create a server, add a listener on 127.0.0.1:5000
- Enable the PowerShell runtime and configuration
- Map a GET route and return a text response

### 2-Multiple-Content-Types: Content negotiation made simple

- Return JSON, XML, YAML, and plain text using dedicated helpers
- See how to call `Write-KrJsonResponse`, `Write-KrXmlResponse`, `Write-KrYamlResponse`, and `Write-KrTextResponse`

### 3-Multi-Language-Routes: Mix languages inline

- Keep your server and plumbing in PowerShell
- Author individual routes in C# or VB.NET using the `-Language` and `-Code` parameters

## Next steps

- Dive into the other Tutorial chapters (Certificates, Logging, Razor Pages, Scheduling)
- Explore the richer example scripts under `examples/PowerShell` (e.g., `MultiRoutes.ps1`)
- Browse the PowerShell cmdlet reference under `docs/pwsh/cmdlets`
- Explore the PowerShell module source at `src/PowerShell/Kestrun`
