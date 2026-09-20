# Camfrog Multi-ID Manager — Production Build

Windows WPF application targeting **.NET 8 / Windows / x64** for managing multiple local Camfrog client processes with separate profile directories and Windows DPAPI-protected stored passwords.

## Authentication and security boundary

This application launches the installed Camfrog client. It does **not** bypass authentication, CAPTCHA, rate limits, licensing, or other access controls, and it does not inject credentials into the client.

The optional **client argument template** supports only:

- `{username}`
- `{profile}`

Use only command-line switches documented and supported by your installed Camfrog client. Stored passwords are protected with Windows DPAPI (`CurrentUser`) and are not automatically injected into the client.

## Production baseline

### Build / solution

- .NET 8 WPF application.
- Correct solution configuration and project graph.
- Normal development builds do not force a RuntimeIdentifier.
- `win-x64` is applied during production publish.
- SQLite dependencies use the Microsoft.Data.Sqlite 8.0.30 line.
- Release builds use nullable reference types, analyzers, and warnings-as-errors.
- Deterministic restore/build/publish workflow is provided.

### Runtime/process lifecycle

- Detects stale/reused PIDs before treating a process as the managed account.
- Tracks process start time and executable path.
- Refuses to terminate a process when identity cannot be verified.
- Graceful close first, then bounded wait, then process-tree termination.
- Does not falsely mark a process stopped when termination fails.
- Periodically reconciles process state with SQLite.
- Avoids duplicate instances for a tracked account.

### Storage/security

- SQLite username uniqueness is enforced case-insensitively.
- SQLite busy timeout and foreign-key enforcement are enabled.
- Passwords are stored outside SQLite using Windows DPAPI CurrentUser protection.
- Secret writes are temporary-file based and cleaned up on failure.
- Account creation rolls back profile/secret state if database insertion fails.
- Settings use safe temporary-file replacement.
- Runtime/event logs are persisted to SQLite and a text log.
- Text logs rotate at approximately 5 MiB.

## Requirements

Build the WPF application on Windows using:

- .NET 8 SDK or a newer compatible SDK.
- Windows desktop targeting components.
- Git.

## Build

From the repository root:

```powershell
dotnet --info
dotnet restore .\CamfrogMultiID.sln
dotnet build .\CamfrogMultiID.sln -c Release --no-restore
dotnet test .\tests\CamfrogMultiID.Tests\CamfrogMultiID.Tests.csproj -c Release --no-restore
```

Run:

```powershell
dotnet run --project .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj -c Release
```

## Production publish

```powershell
.\build-release.ps1
```

The script performs restore, Release build, and self-contained x64 publish.

Output:

```text
src\CamfrogMultiID.App\bin\Release\net8.0-windows\win-x64\publish\
```

## Optional Ubuntu native tooling

For native helper components and native Windows tests:

```bash
bash scripts/bootstrap-ubuntu-mingw-wine-vcpkg.sh
```

This provides MinGW-w64, Wine, vcpkg, and the repository CMake toolchain. It does **not** replace the .NET WPF production build.

## Runtime data

Data is stored under:

```text
%LOCALAPPDATA%\CamfrogMultiID\
```

Files:

- `camfrog.db` — account/runtime/event metadata
- `settings.json` — client executable and argument template
- `profiles\\` — per-account profile directories
- `secrets\\` — DPAPI-protected password blobs
- `logs\\app.log` — operational log
- `logs\\app.log.1` — previous rotated log

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
dotnet nuget locals all --clear
Remove-Item -Recurse -Force .\src\CamfrogMultiID.App\bin, .\src\CamfrogMultiID.App\obj -ErrorAction SilentlyContinue
dotnet restore .\CamfrogMultiID.sln
dotnet build .\CamfrogMultiID.sln -c Release --no-restore
```

### PowerShell execution policy

The production script does not require a permanent execution-policy change. If required:

```cmd
build-release.cmd
```

or for the current PowerShell session only:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\build-release.ps1
```

## Final production validation

Before distribution, verify on a clean Windows test machine:

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
- The exact Camfrog client version intended for release has been manually validated.

See `docs/architecture.md`, `docs/development.md`, `docs/release.md`, and `SECURITY.md` for project-maintainer guidance.
