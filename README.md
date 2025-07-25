
```
██╗  ██╗███████╗███████╗████████╗██████╗ ██╗   ██╗███╗   ██╗
██║ ██╔╝██╔════╝██╔════╝╚══██╔══╝██╔══██╗██║   ██║████╗  ██║
█████╔╝ █████╗  ███████╗   ██║   ██████╔╝██║   ██║██╔██╗ ██║
██╔═██╗ ██╔══╝  ╚════██║   ██║   ██╔██╗  ██║   ██║██║╚██╗██║
██║  ██╗███████╗███████║   ██║   ██║ ██╗ ╚██████╔╝██║ ╚████║
╚═╝  ╚═╝╚══════╝╚══════╝   ╚═╝   ╚═╝ ╚═╝  ╚═════╝ ╚═╝  ╚═══╝
Kestrun — PowerShell brains. Kestrel speed.
```

---

![CI](https://github.com/mdaneri/Kestrun/actions/workflows/dotnet.yml/badge.svg)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

# Kestrun

Kestrun is a hybrid web framework that combines the speed and scalability of ASP.NET Core (Kestrel) with the flexibility and scripting power of PowerShell. It enables you to build web APIs, automation endpoints, and dynamic services using both C# and PowerShell in a single, integrated environment.

## Features

✨ Kestrun Features
	•	🚀 Fast, cross-platform web server
Powered by ASP.NET Core (Kestrel) with full access to advanced HTTP/2, header compression, and TLS options.
	•	🐚 Native PowerShell integration
Routes can be backed by PowerShell scripts with isolated, pooled runspaces and dynamic $Request / $Response variables.
	•	🧠 Multi-language script routing
Register HTTP routes using:
	•	🐚 PowerShell
	•	🧩 C# scripts (Roslyn compiled with typed globals and shared state)
	•	🐍 Python (via Python.NET)
	•	📜 JavaScript (via ClearScript + V8)
	•	🧪 F# (stubbed for future support)
	•	📄 Razor Pages backed by PowerShell
Use .cshtml + .cshtml.ps1 pairs with automatic $Model injection and dynamic rendering via HttpContext.Items["PageModel"].
	•	📦 Modular architecture
Combine C# libraries, PowerShell modules, Razor views, static files, and custom handlers into a unified web app.
	•	🌍 Rich HTTP support
	•	Routes with query, headers, body support
	•	Static files with custom headers, Content-Disposition, stream/async send
	•	Built-in MIME type detection
	•	Charset and compression negotiation
	•	🔐 TLS/HTTPS & Certificate support
	•	Supports X509Certificate2 objects directly
	•	Fine-grained listener control: Protocols, UseConnectionLogging, HTTP/1.1 & HTTP/2
	•	Hot-swap of certificate or listener settings
	•	🧪 Test-friendly architecture
	•	C#: xUnit + script compilation validation (ValidateCSharpScript)
	•	PowerShell: Pester-compatible setup for route and module tests
	•	Script diagnostics: line-numbered errors, detailed exception formatting
	•	🧬 Shared global state
A thread-safe, case-insensitive SharedState store for global variables, usable across C#, PowerShell, and Razor.
	•	🖨️ Flexible response output
Respond with:
	•	WriteTextResponse, WriteJsonResponse, WriteXmlResponse, WriteYamlResponse
	•	WriteFileResponse, WriteBinaryResponse, WriteStreamResponse
	•	Optional Content-Disposition: inline / attachment; filename=…
	•	🧵 Thread-safe runspace pooling
Automatic pooling of PowerShell runspaces with configurable min/max, affinity (PSThreadOptions), and module injection.
	•	📑 Script validation & compilation error reporting
C# route validation returns detailed Roslyn diagnostics without throwing (e.g., for editor integration or CI prechecks).
	•	🧾 Logging with Serilog
	•	Fluent KestrunLoggerBuilder for per-subsystem loggers
	•	Named logger registration & retrieval
	•	Reset/Reload/Dispose support for hot-reload or graceful shutdowns
	•	Default rolling file logs (logs/kestrun.log)
	•	🛠️ CI/CD ready
	•	Build- and run-time configurable
	•	Works in containerized / headless environments
	•	Supports Dev/Prod fallback module path detection
	•	🛡️ Optional Add-ons
Add via fluent extensions:
	•	AddAntiforgery() middleware
	•	AddStaticFiles(), AddDefaultFiles(), AddFileServer()
	•	AddCors(policy) or AddCorsAllowAll()
	•	AddSignalR<T>() for real-time hubs
	•	Ready for Swagger, gRPC, JWT hooks

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [PowerShell 7+](https://github.com/PowerShell/PowerShell)

### Build & Run

Clone the repository:

```pwsh
git clone https://github.com/mdaneri/Kestrun.git
cd Kestrun
```

Build the C# projects:

```pwsh
dotnet build
```

Run an example (e.g., CSharpTest):

```pwsh
dotnet run --project .\examples\CSharp\MultiRoutes\MultiRoutes.csproj
```

### Using the PowerShell Module

Import the module (from source):

```pwsh
Import-Module ./src/PowerShell/Kestrun/Kestrun.psm1
```

## Running Tests

### C# Tests

Tests are written with `xUnit` under `tests/CSharp.Tests/Kestrun.Tests`. To execute them locally:

```pwsh
dotnet test .\tests\CSharp.Tests\Kestrun.Tests\KestrunTests.csproj
```

### PowerShell Tests

PowerShell module tests live under `tests/PowerShell.Tests` and use Pester. Run them locally with:

```pwsh
Invoke-Pester -CI -Path tests/PowerShell.Tests
```

The suite exercises the module's exported commands such as the global variable helpers, path resolution, and response writers.

GitHub Actions runs these tests automatically on every push and pull request.

## Project Structure

- `src/CSharp/` — C# core libraries and web server
  - `Kestrun/Logging` — logging helpers
  - `Kestrun/Hosting` — host configuration
  - `Kestrun/PowerShell` — PowerShell integration
  - `Kestrun/Scripting` — script language helpers
  - `Kestrun/Security` — certificate utilities
  - `Kestrun/Models` — request/response classes
  - `Kestrun/Util` — shared utilities
- `src/PowerShell/` — PowerShell module and scripts
- `examples/` — Example projects (C#, PowerShell)
- `tests/` — Test projects (C#, PowerShell)
- `cert/` — Development certificates
- `Utility/` — Helper scripts
- `docs/` - Documentation

## Contributing

Contributions are welcome! Please open issues or pull requests for bug fixes, features, or documentation improvements.

1. Fork the repo and create your branch
2. Make your changes and add tests
3. Run all tests to verify
4. Submit a pull request

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
