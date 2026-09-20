# Architecture — Camfrog Multi-ID Manager

## System context
Windows WPF desktop application (`.NET 8`, `net8.0-windows`, `win-x64` self-contained) that manages multiple local Camfrog client processes. Each managed identity has a discrete profile directory, DPAPI-protected credential file, and isolated process lifetime. The manager does not bypass Camfrog authentication, CAPTCHA, or licensing and does not auto-inject credentials.

## Components and responsibilities

### CamfrogMultiID.Core (`src/CamfrogMultiID.Core`)
- Domain models: `CamfrogAccount`, `AppSettings`.
- No dependencies on UI, persistence, or OS integration. Pure data contracts and validation.

### CamfrogMultiID.Infrastructure (`src/CamfrogMultiID.Infrastructure`)
- `AppPaths`: deterministic paths under `%LOCALAPPDATA%\CamfrogMultiID` (`camfrog.db`, `profiles/`, `secrets/`, `logs/`, `settings.json`). Supports injected root for testing.
- `DatabaseService`: SQLite (`Microsoft.Data.Sqlite`) with `busy_timeout=5000`, `foreign_keys=ON`, WAL-friendly, case-insensitive `username` uniqueness (`COLLATE NOCASE`), `secret_name` uniqueness, transactions, parameterized queries, bounded lock (`_gate`), migration for `process_executable_path`, disposal-safe connections/commands, best-effort event logging.
- `CredentialService`: Windows DPAPI `CurrentUser` (`ProtectedData`), atomic temp-file writes, zero-memory cleanup for plaintext and protected bytes, no plaintext on disk, fail-safe on corrupt data (throws, caller handles).
- `SettingsService`: JSON (`System.Text.Json`) persistence for `ClientExecutable` and `ClientArgumentsTemplate`, atomic temp-file write, tolerant load (missing/corrupt → defaults), `DataDirectory` always set to `AppPaths.Root`.
- `ProcessSessionService`: process lifecycle with PID validation, start-time and executable-path identity, stale-PID/reuse protection (fail-closed), graceful `CloseMainWindow` → bounded wait (5s) → `Kill(entireProcessTree:true)`, duplicate-instance guard, working-directory isolation, Windows command-line quoting for `{username}`/`{profile}` placeholders.

### CamfrogMultiID.App (`src/CamfrogMultiID.App`)
- WPF UI: `App.xaml`, `MainWindow`, `AccountWindow`, `SettingsWindow`, `app.manifest` (`asInvoker`).
- `App.xaml.cs`: single-instance mutex (`Local\CamfrogMultiID.Manager`), guarded `OnStartup` (no static initialization before `OnStartup`), `Db.Initialize`, emergency log at `startup-error.log`, dispatcher/AppDomain/unobserved-task diagnostics, clean `OnExit` mutex release.
- `MainWindow`: polling reconciler (`DispatcherTimer` 2s) calling `IsTrackedProcessAlive`, DB/UI refresh non-reentrant, log viewer tolerant to I/O failures.
- `AccountWindow`: validation (username required, password required, case-insensitive duplicate), profile/secret creation with rollback on DB failure, DPAPI save before DB insert.
- `SettingsWindow`: executable browse, existence validation, argument template support (`{username}`, `{profile}` only).

### CamfrogMultiID.Tests (`tests/CamfrogMultiID.Tests`)
- xUnit, `net8.0-windows`, covers DatabaseService, CredentialService, SettingsService, ProcessSessionService (identity, quoting, start/stop, stale PID).

## Data/storage model
```
%LOCALAPPDATA%\CamfrogMultiID\
  camfrog.db          # SQLite: accounts, events
  settings.json       # AppSettings
  profiles\<guid>\    # per-account profile directory
  secrets\<name>.bin  # DPAPI blobs
  logs\app.log        # rotated at ~5 MiB → app.log.1
  startup-error.log   # emergency startup failures
```
- SQLite schema: `accounts(id PK, display_name, username, secret_name UNIQUE, profile_directory, enabled, status, process_id, started_utc, process_executable_path, last_error)` + unique index `ux_accounts_username` (`COLLATE NOCASE`) + `events`.
- Secrets never in SQLite, never in logs, never in command-line.

## External integrations
- Local filesystem, SQLite, Windows DPAPI, `System.Diagnostics.Process`, Win32 `MainModule`, `CloseMainWindow`.
- No network, no cloud, no auto-update.

## Authentication and authorization
- Credentials stored via DPAPI `CurrentUser`; only the same Windows user can decrypt.
- No elevation; `asInvoker`.
- Single-instance mutex prevents concurrent managers.

## Trust boundaries
- Untrusted: user-supplied `ClientExecutable`, `ClientArgumentsTemplate`, `Username`, `ProfileDirectory`.
- Validation: executable must exist (`File.Exists` after `Path.GetFullPath`), template limited to two placeholders, arguments quoted, profile directory created under controlled root, username uniqueness enforced.

## Deployment topology
- Self-contained `win-x64`, single-file `CamfrogMultiID.exe` (see `build-release.ps1`).
- No container, no service, no installer; copy to Windows x64 and run.

## Observability
- DB `events` + text log, startup log, UI log viewer, process start/stop logs with PID and reason, failure logs with sanitized messages.

## Availability and recovery
- DB best-effort logging never crashes manager, atomic writes, rollback on account creation failure, graceful process termination with timeouts, stale-PID detection, crash recovery via reconciler.

## Security considerations
- DPAPI scope `CurrentUser`, zero-memory, no plaintext persistence, parameterized SQL, path canonicalization, process identity validation, fail-closed on reuse, least-privilege workflows, dependency review, CodeQL `csharp`.

## Known constraints
- Client may ignore `{profile}`; then instances share state.
- Password not auto-injected.
- Requires Windows, .NET 8, `%LOCALAPPDATA%` writable.

Record material decisions as ADRs under `docs/adr/`.
