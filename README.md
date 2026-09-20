# Camfrog Multi-ID Manager — Production Build Baseline

Windows WPF application targeting **.NET 8 / Windows / x64** for managing multiple local Camfrog client processes with separate profile directories and Windows DPAPI-protected stored passwords.

## Authentication and security boundary

This application launches the installed Camfrog client. It does **not** bypass authentication, CAPTCHA, rate limits, licensing, or other access controls, and it does not inject credentials into the client.

The optional argument template supports only:

- `{username}`
- `{profile}`

Use only command-line switches documented and supported by your installed Camfrog client. The stored password is protected with Windows DPAPI (`CurrentUser`) and is not automatically injected into the client.

## Production fixes in this build

### Build / solution
- Fixed the broken `.sln` structure: `ProjectConfigurationPlatforms` now has its required `EndGlobalSection`.
- Targets .NET 8 explicitly.
- Normal solution builds do not force a RuntimeIdentifier; `win-x64` is applied only during production publish. This avoids RID-specific NuGet asset mismatches during ordinary development builds.
- SQLite dependencies are pinned to the stable 8.0.30 Microsoft.Data.Sqlite line and its native SQLite bundle.
- WPF application is configured for `win-x64`.
- Release build uses warnings-as-errors and nullable reference types.
- Added deterministic restore/build/publish PowerShell workflow.

### Runtime/process lifecycle
- Detects stale/reused PIDs before treating a process as the managed account.
- Tracks process start time and executable path.
- Refuses to terminate a process when the tracked identity does not match.
- Graceful close first, then bounded wait, then process-tree termination.
- Does not falsely mark a process stopped when termination fails.
- Periodically reconciles process state with SQLite.
- Start operations avoid duplicate instances for a tracked account.

### Storage/security
- SQLite username uniqueness is enforced case-insensitively.
- SQLite busy timeout and foreign-key enforcement are enabled.
- Existing databases are migrated forward with the new process metadata column.
- Passwords are stored outside SQLite using Windows DPAPI CurrentUser protection.
- Secret writes are temporary-file based and cleaned up on failure.
- Account creation rolls back the profile and secret if database insertion fails.
- Settings are written through a temporary file and cleaned up after replacement.
- Runtime/event logs are persisted to SQLite and a text log.
- Text log rotates at approximately 5 MiB.

### Application reliability
- Single-instance mutex handling is safe for abandoned and non-owner cases.
- Startup failures are logged and surfaced.
- UI refresh avoids re-entrant refresh operations.
- File access failures in the log viewer do not crash the UI.

## Requirements

Build on Windows using either:

- .NET 8 SDK, or
- a newer .NET SDK capable of targeting `net8.0-windows`.

For WPF builds, use a Windows machine with the Windows desktop targeting components available.

## Build

From the repository root:

```powershell
dotnet --info
dotnet restore .\CamfrogMultiID.sln
dotnet build .\CamfrogMultiID.sln -c Release --no-restore
```

Run:

```powershell
dotnet run --project .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj -c Release
```

## Production publish

Use:

```powershell
.\build-release.ps1
```

The script performs restore, Release build, and self-contained x64 publish.

Output:

```text
src\CamfrogMultiID.App\bin\Release\net8.0-windows\win-x64\publish\
```

The publish is configured as a self-contained Windows x64 application with a single-file executable.

## Runtime data

Data is stored under:

```text
%LOCALAPPDATA%\CamfrogMultiID\
```

Files:

- `camfrog.db` — account/runtime/event metadata
- `settings.json` — client executable and argument template
- `profiles\` — per-account profile directories
- `secrets\` — DPAPI-protected password blobs
- `logs\app.log` — operational log
- `logs\app.log.1` — previous rotated log

## First-run procedure

1. Start `CamfrogMultiID.exe`.
2. Open **Settings**.
3. Select the installed Camfrog executable.
4. Configure only documented client arguments if required.
5. Add an account.
6. Use **Start Selected** to launch one account.
7. Confirm the client behaves correctly.
8. Use **Start All** only after individual process behavior is verified.

The manager does not assume that the installed Camfrog version accepts login/profile command-line switches.

## Important operational notes

- Separate profile directories are created for each managed account, but whether the Camfrog client actually honors the directory depends on the supported client behavior.
- If the client ignores `{profile}`, multiple instances may still share client state.
- A stored password is not automatically submitted to Camfrog.
- Do not place plaintext passwords in `settings.json`, command-line arguments, logs, or scripts.
- The application intentionally runs without administrator elevation (`asInvoker`).

## Troubleshooting

### Solution parser error

If you previously saw:

```text
Solution file error MSB5007:
Error parsing the project configuration section in solution file.
The entry "EndGlobal" is invalid.
```

the original solution was missing the `EndGlobalSection` terminator for `ProjectConfigurationPlatforms`. This package contains the corrected structure.

### Verify the solution

```powershell
dotnet sln .\CamfrogMultiID.sln list
```

Expected projects:

```text
CamfrogMultiID.Core
CamfrogMultiID.Infrastructure
CamfrogMultiID.App
CamfrogMultiID.Tests
```

### Clean rebuild

```powershell
dotnet clean .\CamfrogMultiID.sln -c Release
Remove-Item -Recurse -Force .\src\CamfrogMultiID.App\bin, .\src\CamfrogMultiID.App\obj -ErrorAction SilentlyContinue
dotnet restore .\CamfrogMultiID.sln
dotnet build .\CamfrogMultiID.sln -c Release --no-restore
```

## Final production check

Before distributing the application, verify on a clean Windows test machine:

- Release build completes with zero warnings/errors.
- Self-contained x64 publish starts.
- Settings persist after restart.
- One account starts and stops correctly.
- Two accounts do not overwrite each other's profile directories.
- Duplicate usernames are rejected case-insensitively.
- A terminated client is detected by the manager.
- A reused PID is not accidentally terminated.
- DPAPI secrets are inaccessible to a different Windows user.
- Application logs contain no plaintext password.


## V5 restore/build workflow

If `dotnet restore .\CamfrogMultiID.sln` reports success but the following build says:

```text
NETSDK1004: Assets file '...\CamfrogMultiID.App\obj\project.assets.json' not found
```

restore the application project directly:

```powershell
dotnet restore .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj --force-evaluate
```

Then verify:

```powershell
Test-Path .\src\CamfrogMultiID.App\obj\project.assets.json
```

It must return:

```text
True
```

Then:

```powershell
dotnet build .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj -c Release --no-restore
```

The V5 `build-release.ps1` performs this explicit graph restore automatically before building.

For a completely clean V5 build:

```powershell
dotnet clean .\CamfrogMultiID.sln
dotnet nuget locals all --clear

Remove-Item -Recurse -Force .\src\CamfrogMultiID.App\bin, .\src\CamfrogMultiID.App\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\src\CamfrogMultiID.Infrastructure\bin, .\src\CamfrogMultiID.Infrastructure\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\src\CamfrogMultiID.Core\bin, .\src\CamfrogMultiID.Core\obj -ErrorAction SilentlyContinue

dotnet restore .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj --force-evaluate
dotnet build .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj -c Release --no-restore
```


## V6 analyzer fixes

The Infrastructure project is built with analyzers enabled and warnings treated as errors. V6 fixes:

- CA1305: database scalar conversion now uses `CultureInfo.InvariantCulture`.
- CA1822: `IsTrackedProcessAlive` is declared `static` because it has no instance-state dependency.

Build directly with:

```powershell
dotnet build .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj -c Release --no-restore
```

Do not add `NoWarn` or disable analyzers to work around compiler/analyzer failures; production builds should keep these checks enabled.


## V7 App compilation fixes

- Explicit `System.IO` imports were added to WPF code-behind files because the App project does not rely on implicit framework namespaces for `File`, `Path`, `Directory`, or `IOException`.
- `IsTrackedProcessAlive` remains an instance method so existing `App.Sessions.IsTrackedProcessAlive(...)` calls are valid.
- `StartAccount` and `StopAccount` are static because they use only application-wide services and their parameters, satisfying CA1822 without changing the public process-service API.


## V13 test and solution regeneration

- Regenerated `CamfrogMultiID.sln` via `dotnet new sln` to fix hidden solution-configuration corruption that caused `dotnet restore` to skip `Infrastructure`/`App` (`BuildProjectInSolution=False`) and required a manual `win-x64` workaround. The new sln correctly maps `Debug|Any CPU` and `Release|Any CPU` for all projects.
- Added `tests/CamfrogMultiID.Tests` (xUnit, `net8.0-windows`, 39 tests) covering database, DPAPI, settings, process identity, quoting, and concurrency.
- CI now runs on `windows-latest` with `setup-dotnet`, `restore` (solution + `win-x64`), `build` Debug/Release, `test`, `publish` single-file, and artifact upload.
- CodeQL now scans `csharp` (windows) and `actions` (ubuntu) with `security-extended`.
- `.gitignore` now excludes `bin/`, `obj/`, `.vs/`, `TestResults/`, `publish/`.
- `Makefile` and `Dockerfile` updated to reflect actual Windows WPF lifecycle.
- Docs `docs/architecture.md`, `docs/development.md`, `docs/release.md`, `docs/template-inventory.md` synchronized.

## PowerShell execution policy

The production script does not require changing the machine-wide execution policy. If PowerShell blocks `build-release.ps1` because it is unsigned, use the included `build-release.cmd` launcher:

```cmd
build-release.cmd
```

It invokes PowerShell with `-ExecutionPolicy Bypass` for that single build process only. It does not permanently change the user's or machine's execution policy.

Alternatively, for the current PowerShell session only:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\build-release.ps1
```
