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

Tests are written with `xUnit` under `tests/KestrunLib.Tests`. To execute them locally:

```bash
dotnet test tests/KestrunLib.Tests/KestrunLib.Tests.csproj
```

PowerShell module tests live under `tests/PowerShell.Tests` and use Pester.
Run them locally with:

```powershell
pwsh -NoLogo -Command "Invoke-Pester -CI -Path tests/PowerShell.Tests"
```

A GitHub Actions workflow runs these tests automatically on every push and pull request.
