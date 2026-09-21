# Development — Camfrog Multi-ID Manager

## Prerequisites
- Windows 10/11 x64
- .NET SDK 8.0.425+ (`dotnet --info` should show `8.0.4xx` and `Microsoft.WindowsDesktop.App 8.0.x`)
- No admin required; WPF builds need Windows desktop workload (included in .NET 8 SDK).

## Local setup
```powershell
git clone https://github.com/cvsz/zcamfrog.git
cd zcamfrog
dotnet --info
dotnet restore CamfrogMultiID.sln
dotnet restore src/CamfrogMultiID.App/CamfrogMultiID.App.csproj --force-evaluate -r win-x64
```

## Quality gates (must pass before PR)
```powershell
dotnet build CamfrogMultiID.sln -c Debug --no-restore
dotnet build CamfrogMultiID.sln -c Release --no-restore
# fallback if solution restore was incomplete:
dotnet build src/CamfrogMultiID.App/CamfrogMultiID.App.csproj -c Release --no-restore
dotnet test CamfrogMultiID.sln -c Release --no-build --verbosity normal
```

## Publish (production artifact)
```powershell
.\build-release.ps1
# or
.\build-release.cmd
```
Output: `src/CamfrogMultiID.App/bin/Release/net8.0-windows/win-x64/publish/CamfrogMultiID.exe` (self-contained, single-file).

## Diagnostic run
```powershell
.\run-diagnostic.cmd
# checks that publish exists and process stays alive; logs at %LOCALAPPDATA%\CamfrogMultiID\
```

## Formatting and analyzers
- `Directory.Build.props` enables `EnableNETAnalyzers`, `AnalysisLevel latest-recommended`, `Deterministic`, `ContinuousIntegrationBuild`.
- All projects use `TreatWarningsAsErrors=true`, `Nullable=enable`, `ImplicitUsings=enable`.
- Use modern APIs: `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIfNegativeOrZero` where applicable.

## Testing
- `tests/CamfrogMultiID.Tests` (xUnit, net8.0-windows) covers database, credentials (DPAPI), settings, process identity, quoting, concurrency.
- Tests use isolated temp roots via `new AppPaths(Path.GetTempPath()+Guid)`; no pollution of real `%LOCALAPPDATA%`.
- Run: `dotnet test -c Release`

## Troubleshooting
- `NETSDK1004: project.assets.json not found` → run the two-step restore above (solution + win-x64 graph). `build-release.ps1` does this automatically.
- WPF window not showing → check `%LOCALAPPDATA%\CamfrogMultiID\startup-error.log` and `logs\app.log`; verify `<InvariantGlobalization>false` (required for WPF).

## Documentation
Update `docs/architecture.md`, `docs/development.md`, `docs/release.md`, and `README.md` when behavior changes. Record decisions in `docs/adr/`.

## Security
- Do not commit secrets, do not log plaintext passwords, do not put secrets in `settings.json` or command-line.
- Keep CodeQL and dependency-review enabled.
