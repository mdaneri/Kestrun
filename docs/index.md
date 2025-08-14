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
- 📚 **Tutorials**: [dpwsh/tutorial/](./pwsh/tutorial/)

## Get started

```powershell
# spin up Kestrun
Import-Module Kestrun
Start-Kestrun -Path ./KestrunApp
```
