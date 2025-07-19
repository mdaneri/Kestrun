
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

- 🚀 Fast, cross-platform web server (Kestrel)
- 🐚 Native PowerShell scripting integration
- 🔗 Expose PowerShell functions as HTTP endpoints
- 🔒 Certificate and HTTPS support
- 📦 Modular architecture: C# libraries, PowerShell modules, and examples
- 🧪 Comprehensive test suite (xUnit for C#, Pester for PowerShell)
- 🛠️ CI/CD with GitHub Actions

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
- `src/PowerShell/` — PowerShell module and scripts
- `examples/` — Example projects (C#, PowerShell)
- `tests/` — Test projects (C#, PowerShell)
- `cert/` — Development certificates
- `Utility/` — Helper scripts

## Contributing

Contributions are welcome! Please open issues or pull requests for bug fixes, features, or documentation improvements.

1. Fork the repo and create your branch
2. Make your changes and add tests
3. Run all tests to verify
4. Submit a pull request

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
