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

A GitHub Actions workflow runs these tests automatically on every push and pull request.
