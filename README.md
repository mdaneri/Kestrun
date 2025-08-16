
<p align="center">
  <img src="docs/assets/kestrun-wordmark.svg" alt="Kestrun wordmark" width="400"/>
</p>
<p align="center"><em>PowerShell brains. Kestrel speed</em></p>

---

![CI](https://github.com/Kestrun/Kestrun/actions/workflows/dotnet.yml/badge.svg)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)


Kestrun is a hybrid web framework that combines the speed and scalability of ASP.NET Core (Kestrel) with the flexibility and scripting power of PowerShell. It enables you to build web APIs, automation endpoints, and dynamic services using both C# and PowerShell in a single, integrated environment.

## Core Capabilities

- **🚀 Fast, cross-platform web server**  
  Powered by **ASP.NET Core (Kestrel)** with full access to advanced HTTP/2, header compression, and TLS options.

- **🐚 Native PowerShell integration**  
  Routes can be backed by PowerShell scripts with isolated, pooled **runspaces** and dynamic `$Context.Request` / `$Context.Response` variables.

- **🧠 Multi-language script routing**  
  Register HTTP routes using:
  - 🐚 PowerShell  
  - 🧩 C# scripts (Roslyn compiled with typed globals and shared state)  
  - � VB.NET scripts (full .NET scripting with claims and validation support)  
  - �🐍 Python (via Python.NET)  
  - 📜 JavaScript (via ClearScript + V8)  
  - 🧪 F# (stubbed for future support)

- **📄 Razor Pages backed by PowerShell**  
  Use `.cshtml + .cshtml.ps1` pairs with automatic `$Model` injection and dynamic rendering via `HttpContext.Items["PageModel"]`.

- **📦 Modular architecture**  
  Combine C# libraries, PowerShell modules, Razor views, static files, and custom handlers into a unified web app.

## HTTP & Protocol Support

- **🌍 Rich HTTP support**  
  - Routes with query, headers, body support  
  - Static files with custom headers, `Content-Disposition`, stream/async send  
  - Built-in MIME type detection  
  - Charset and compression negotiation

- **🔐 TLS/HTTPS & Certificate support**  
  - Supports `X509Certificate2` objects directly  
  - Fine-grained listener control: `Protocols`, `UseConnectionLogging`, HTTP/1.1 & HTTP/2  
  - Hot-swap of certificate or listener settings

- **🛡️ Comprehensive Authentication & Authorization**  
  - **Multiple authentication schemes**: Windows, Basic, API Key, JWT Bearer, Cookie, Certificate, Negotiate, OpenID Connect  
  - **Claims-based authorization**: Rich claim policies with PowerShell and VB.NET claim providers  
  - **Route-level authorization**: Fine-grained access control per endpoint  
  - **Credential validation**: Supports SecureString utilities and custom validation delegates

## Developer-Focused

- **🧪 Test-friendly architecture**  
  - **C#**: xUnit + script compilation validation (`ValidateCSharpScript`)  
  - **PowerShell**: Pester-compatible setup for route and module tests  
  - Script diagnostics: line-numbered errors, detailed exception formatting

- **🧬 Shared global state**  
  A thread-safe, case-insensitive `SharedState` store for global variables, usable across C#, PowerShell, and Razor.

- **🖨️ Flexible response output**  
  Respond with:
  - `WriteTextResponse`, `WriteJsonResponse`, `WriteXmlResponse`, `WriteYamlResponse`  
  - `WriteFileResponse`, `WriteBinaryResponse`, `WriteStreamResponse`  
  - Optional `Content-Disposition: inline` / `attachment; filename=…`

- **🧵 Thread-safe runspace pooling**  
  Automatic pooling of PowerShell runspaces with configurable min/max, affinity (`PSThreadOptions`), and module injection.

- **📑 Script validation & compilation error reporting**  
  C# route validation returns detailed Roslyn diagnostics without throwing (e.g., for editor integration or CI prechecks).

- **🧾 Logging with Serilog**  
  - Fluent `KestrunLoggerBuilder` for per-subsystem loggers  
  - Named logger registration & retrieval  
  - Reset/Reload/Dispose support for hot-reload or graceful shutdowns  
  - Default rolling file logs (`logs/kestrun.log`)

## Deployment & Extensibility

- **🛠️ CI/CD ready**  
  - Build- and run-time configurable  
  - Works in containerized / headless environments  
  - Supports Dev/Prod fallback module path detection

- **🛡️ Optional Add-ons**  
  Add via fluent extensions:
  - `AddAntiforgery()` middleware  
  - `AddStaticFiles()`, `AddDefaultFiles()`, `AddFileServer()`  
  - `AddCors(policy)` or `AddCorsAllowAll()`  
  - `AddSignalR<T>()` for real-time hubs  
  - `AddAuthentication()` with multiple schemes (Windows, Basic, JWT, Certificate, etc.)  
  - Ready for Swagger, gRPC, custom middleware hooks

- **⚡ Task Scheduling & Background Jobs**  
  - **Cron-based scheduling**: Full cron expression support via Cronos  
  - **Multi-language job support**: Schedule PowerShell, C#, and VB.NET scripts as background jobs  
  - **Job management**: Start, stop, and monitor scheduled tasks with detailed logging

## Getting Started

### Prerequisites

**For Building:**

- [.NET 8 SDK](https://dotnet.microsoft.com/download) AND [.NET 9 SDK](https://dotnet.microsoft.com/download) (both required)
- **PowerShell 7.4+** or **PowerShell 7.5+** - Download from [PowerShell GitHub Releases](https://github.com/PowerShell/PowerShell/releases)
- **InvokeBuild** and **Pester** PowerShell modules:

```pwsh
Install-Module InvokeBuild, Pester -Force
```

**For Runtime:**

- [.NET 8 Runtime](https://dotnet.microsoft.com/download) or [.NET 9 Runtime](https://dotnet.microsoft.com/download)
- **PowerShell 7.4+** (requires .NET 8) or **PowerShell 7.5+** (requires .NET 9)
  - Download from [PowerShell GitHub Releases](https://github.com/PowerShell/PowerShell/releases)

### Build & Run

Clone the repository:

```pwsh
git clone https://github.com/Kestrun/Kestrun.git
cd Kestrun
```

Build the solution using Invoke-Build:

```pwsh
# Build only
Invoke-Build Build

# Or build, test, and clean in one command
Invoke-Build All
```

Run an example (e.g., MultiRoutes):

```pwsh
dotnet run --project .\examples\CSharp\MultiRoutes\MultiRoutes.csproj
```

### Using the PowerShell Module

Import the module (from source):

```pwsh
Import-Module ./src/PowerShell/Kestrun/Kestrun.psm1
```

## Running Tests

### Using Invoke-Build (Recommended)

The project includes an Invoke-Build script that automatically handles both C# (xUnit) and PowerShell (Pester) tests:

```pwsh
# Run all tests (both C# and PowerShell)
Invoke-Build Test

# Or run the complete build pipeline (clean, build, and test both C# and PowerShell)
Invoke-Build All
```

### Manual Test Execution

If you need to run tests individually:

#### C# Tests

Tests are written with `xUnit` under `tests/CSharp.Tests/Kestrun.Tests`. To execute them manually:

```pwsh
dotnet test .\tests\CSharp.Tests\Kestrun.Tests\KestrunTests.csproj
```

#### PowerShell Tests

PowerShell module tests live under `tests/PowerShell.Tests` and use Pester. Run them manually with:

```pwsh
Invoke-Pester -CI -Path tests/PowerShell.Tests
```

The suite exercises the module's exported commands such as the global variable helpers, path resolution, and response writers.

GitHub Actions runs these tests automatically on every push and pull request.

## Project Structure

- `src/CSharp/` — C# core libraries and web server
  - `Kestrun/Authentication` — authentication handlers and schemes
  - `Kestrun/Certificates` — certificate management utilities
  - `Kestrun/Hosting` — host configuration and extensions
  - `Kestrun/Languages` — multi-language scripting support (C#, VB.NET, etc.)
  - `Kestrun/Logging` — Serilog integration and logging helpers
  - `Kestrun/Middleware` — custom middleware components
  - `Kestrun/Models` — request/response classes and data models
  - `Kestrun/Razor` — Razor Pages integration with PowerShell
  - `Kestrun/Scheduling` — task scheduling and background job support
  - `Kestrun/Scripting` — script execution and validation
  - `Kestrun/Security` — security utilities and helpers
  - `Kestrun/SharedState` — thread-safe global state management
  - `Kestrun/Utilities` — shared utility functions
- `src/PowerShell/` — PowerShell module and scripts
- `examples/` — Example projects and demonstrations
  - `CSharp/Authentication` — authentication examples
  - `CSharp/Certificates` — certificate usage examples
  - `CSharp/HtmlTemplate` — HTML templating examples
  - `CSharp/MultiRoutes` — multi-route examples
  - `CSharp/RazorSample` — Razor Pages examples
  - `CSharp/Scheduling` — task scheduling examples
  - `CSharp/SharedState` — shared state examples
  - `PowerShell/` — PowerShell examples
  - `Files/` — test files and resources
- `tests/` — Test projects (C#, PowerShell)
- `cert/` — Development certificates
- `docs/` — Documentation files
- `Utility/` — Build and maintenance scripts
- `.github/` — GitHub Actions workflows
- `Lint/` — Code analysis rules

## Contributing

Contributions are welcome! Please open issues or pull requests for bug fixes, features, or documentation improvements.

1. Fork the repo and create your branch
2. Make your changes and add tests
3. Run all tests to verify
4. Submit a pull request

## License

This project is licensed under the MIT License (SPDX: MIT). See [LICENSE](LICENSE) for details.
