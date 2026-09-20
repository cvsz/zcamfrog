# Development

## Requirements

- Windows 10/11 x64.
- .NET 8 SDK or newer SDK capable of targeting `net8.0-windows`.
- Git.
- PowerShell 7 is recommended for release automation.

## Build and test

```powershell
dotnet restore .\CamfrogMultiID.sln
dotnet build .\CamfrogMultiID.sln -c Release --no-restore
dotnet test .\tests\CamfrogMultiID.Tests\CamfrogMultiID.Tests.csproj -c Release --no-restore
```

Run locally:

```powershell
dotnet run --project .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj -c Release
```

Production publish:

```powershell
.\build-release.ps1
```

## Quality gates

Before opening a pull request:

1. Build Release with warnings treated as errors.
2. Run regression tests.
3. Run `dotnet format --verify-no-changes`.
4. Run repository security marker checks.
5. Confirm no secrets or local profile/database artifacts are tracked.
6. Review process lifecycle changes for PID reuse and fail-closed behavior.

## Testing strategy

Infrastructure tests cover case-insensitive username uniqueness, DPAPI credential round trips, settings normalization/persistence, and deletion safety for running accounts.

Changes to process identity or termination must add regression coverage where practical and must never weaken identity checks just to make tests pass.
