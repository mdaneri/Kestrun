---
title: Home
layout: home
nav_order: 1
description: "Kestrun Documentation."
permalink: /
---

# Kestrun

**Kestrun** is a PowerShell-integrated web framework on ASP.NET Core (Kestrel) — blend C# power with the sensual flow of PowerShell scripts.

## Highlights

- **PowerShell-first routing** — author endpoints with `Add-KrMapRoute` or Razor+PS hybrids.
- **Auth built-ins** — JWT, API keys, Kerberos, client certs.
- **Razor + PS** — serve `.cshtml` with `.ps1` backers.
- **Scheduling** — PowerShell and C# jobs with cron-like control.
- **Logging** — Serilog, syslog, REST; structured logs galore.
- **OpenAPI** — generate specs, validate I/O.
- **WebDAV, SMTP/FTP** — expand beyond HTTP when you want to get naughty.

## Quick links

- 👉 **PowerShell Cmdlets**: [pwsh/cmdlets/](./pwsh/cmdlets/)
- 👉 **C# API**: [cs/api/](./cs/api/)
- 📚 **Tutorials**: [pwsh/tutorial/](./pwsh/tutorial/)

## Get started

```powershell
# spin up Kestrun
Import-Module Kestrun
New-KrServer -Name 'MyKestrunServer'
Add-KrListener -Port 5000
Add-KrPowerShellRuntime
Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Path '/ps/hello' -ScriptBlock {
    Write-KrTextResponse -inputObject "Hello world" -statusCode 200
}
Add-KrMapRoute -Verbs Get -Path '/cs/hello' -Code @'
    Context.Response.WriteTextResponse("Hello world", 200);
'@ -Language CSharp

Start-KrServer

```
