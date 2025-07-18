```
██╗  ██╗███████╗███████╗████████╗██████╗ ██╗   ██╗███╗   ██╗
██║ ██╔╝██╔════╝██╔════╝╚══██╔══╝██╔══██╗██║   ██║████╗  ██║
█████╔╝ █████╗  ███████╗   ██║   ██████╔╝██║   ██║██╔██╗ ██║
██╔═██╗ ██╔══╝  ╚════██║   ██║   ██╔██╗  ██║   ██║██║╚██╗██║
██║  ██╗███████╗███████║   ██║   ██║ ██╗ ╚██████╔╝██║ ╚████║
╚═╝  ╚═╝╚══════╝╚══════╝   ╚═╝   ╚═╝ ╚═╝  ╚═════╝ ╚═╝  ╚═══╝
              Kestrun — PowerShell brains. Kestrel speed.
```

## Running Tests

Tests are written with `xUnit` under `tests/Kestrun.Tests`. To execute them locally:

```bash
dotnet test tests/Kestrun.Tests/Kestrun.Tests.csproj
```

PowerShell module tests live under `tests/PowerShell.Tests` and use Pester.
Run them locally with:

```powershell
pwsh -NoLogo -Command "Invoke-Pester -CI -Path tests/PowerShell.Tests"
```

The suite exercises the module's exported commands such as the global
variable helpers, path resolution and response writers.

A GitHub Actions workflow runs these tests automatically on every push and pull request.
