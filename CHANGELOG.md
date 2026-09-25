# Changelog

All notable changes to Camfrog Multi-ID Manager are documented here. Based on Keep a Changelog and Semantic Versioning.

## [1.3.0] - 2026-09-25
### Added
- Health dashboard (per-account running/error lines, 30-event feed)
- One-click elevated Sandboxie box creation with UAC prompt
- Version 1.3.0 stamped into the executable and window title

### Fixed
- Stop now terminates Sandboxie box contents (`/terminate`) instead of only killing `Start.exe`
- Sandboxie boxes created via `SbieIni` (probing with `Start.exe` never persisted); underscore box names preserved and unquoted
- Sandboxie readiness gate (driver/service check) with plain-language errors

## [1.1.0] - 2026-09-24
### Added
- Pre-start foreign-client warning: starting while an untracked copy of the client runs now asks first (single-instance handoff exits in ~1s otherwise); tray-minimized copies called out explicitly
- `FindForeignClientProcesses` (identity-verified, warning-only, never kills) + Thai/English strings
- App version 1.1.0 stamped into the executable (Company/Product/Copyright) and shown in the window title + startup log

### Changed
- `ABOUT.md`, `GOVERNANCE.md`, `CONTRIBUTING.md`, `.env.example`, `CODEOWNERS` rewritten from template-generic to project-specific; unused `FUNDING.yml` removed

## [Unreleased]
### Added
- Online/offline + room dashboard: Presence/Room grid columns, honest join evidence (live client command lines scanned for the room link; server-side membership correctly reported as unobservable), room name parsing

### Fixed
- Stop left sandboxed clients running: killing `Start.exe` does not stop a Sandboxie box. Stop now runs `Start.exe /Box:<name> /terminate` first (verified live), then falls back to process-tree kill
- Mojibake repair: em-dashes typed through an editing channel had corrupted into Thai glyphs in code; replaced by codepoint, repo-wide scan clean (only legitimate Thai remains)

### Added
- Thai translation repair: 118 values damaged by an editing channel rewritten verified-clean (0 control chars, exact codepoints); regression test guards the class; Thai-capable font fallback on log/details panes
- Auto-start: `AutoStartAccounts` setting starts enabled accounts silently at manager launch; `TryStartSandboxieService` best-effort SbieSvc start (elevation-gated) wired into startup when sandboxing is on
- Health dashboard: per-account running/error lines with uptime plus a live 30-event feed; `GetRecentEvents` API
- One-click elevated box creation: Settings "Create Boxes (Admin...)" relaunches the box setup with a UAC prompt (`-EncodedCommand`, no quoting hazards); empty/duplicate box lists handled
- Account lifecycle: Edit (display/username/enabled + optional password replace), Delete with confirmation + secret/profile cleanup, Enable/Disable toggle, Change Password dialog, Clear Error
- Main window: search/filter, selected-account details + launch preview, status bar (counts + client status), log level filter + tail 500 + auto-scroll + clear/open-folder, context menu, double-click edit, F5/Delete/Enter shortcuts, Start All / Stop All confirmations
- Settings: exe existence status, template warning validation, data folder display + open-folder
- Backend: `GetById`, `UpdateDetails`, `SetEnabled`, `Delete`, `ClearError`, `UsernameExistsExcept`, `CredentialService.Exists/Delete`, `PreviewCommandLine/PreviewArguments/ValidateArgumentsTemplate`
- Dependencies (verified `net8.0-windows`): `Microsoft.Data.Sqlite` 8.0.30→10.0.12, `ProtectedData` 8.0.0→10.0.12, `Test.Sdk` 17.8.0→18.10.1, `coverlet` 6.0.0→10.0.1, `xunit` 2.5.3→2.9.3, `runner.visualstudio` 2.5.3→4.0.0; Actions `checkout` v4→v7, `setup-dotnet` v4→v6, `upload-artifact` v4→v7
- Tests: 86 xUnit tests (DatabaseService, CredentialService, SettingsService, ProcessSessionService, backup, diagnostics, localization, restart policy)
- Startup crash fix: guard `MainWindow` event handlers during `InitializeComponent` (`_initialized`), corrupt dates parse to null instead of throwing, refresh loop catches and logs instead of killing the UI
- Sandbox multi-instance (experimental): optional Sandboxie-Plus launch per account (`Start.exe /wait /Box:<user>`, box name sanitized); env-redirection alone proven insufficient (client mutex)
- Auto-join rooms: per-account `RoomUrl` (`camfrog:` scheme validated) appended as `--url=` (registry-verified client switch); launch preview shows full sandboxed command
- Sandboxie onboarding: status + one-click download + Create-Boxes-For-All in Settings, box state in details panel, service-readiness gate, version detection, `bundle-setup.ps1/.cmd`, `docs/sandboxie.md`
- Auto-restart (opt-in per account, `RestartPolicy`: 3 per 10 min, loop pauses to Error, silent timer path, budget reset on manual start/stop)
- Backup/restore in Settings (zip of DB snapshot + secrets + settings; traversal validation; `ClearAllPools` so live DB restores) plus scheduled auto-backups with pruning
- Secrets-dir ACL restricted to current user; log export; diagnostics bundle; screen-reader names; release provenance attestation
- Deps: `System.IO.FileSystem.AccessControl` 5.0.0 (latest stable; no 8.x exists), `System.ServiceProcess.ServiceController` 8.0.0
- Health: uptime + restart-window counts in details; password-age with 90-day rotation notice
- `package-msix.ps1` path-ready (SDK-gated, unverified locally)
- Localization: English + Thai (154 resx keys, hand-written accessor without designer dependency, `x:Static` XAML, `Language` setting, startup `CurrentUICulture`, `L10n.Fmt` helper satisfying CA1863/CA1304); real-process start/stop test via cmd fake client
- CI `build` job on `windows-latest` (.NET 8): restore, Debug, Release, test, publish `win-x64` self-contained, artifact upload
- CodeQL matrix for `csharp` (windows) and `actions` (ubuntu); Dependabot `nuget` ecosystem
- Docs: architecture, development, release, troubleshooting, sandboxie, telemetry proposal; project inventory; project-specific agent contract

### Changed
- `.gitignore` now excludes .NET artifacts (`bin/`, `obj/`, `.vs/`, `TestResults/`, `publish/`, `coverage/`, `*_wpftmp.csproj`, secrets)
- `Makefile` targets now run real `dotnet` commands (setup, format, lint, test, build, publish)
- `Dockerfile` documents Windows-only WPF (not supported in Linux container)
- `tests` added to solution; `CamfrogMultiID.sln` regenerated with valid GUIDs

### Fixed
- Auto-restart loop guard never tripped: budget was reset on every successful (re)start; now resets only on manual start/stop
- Sandboxie "Invalid box name parameter" (Sbie 3204): unquoted box names + auto-ensure box exists; readiness gate for missing driver/service
- Sandboxie box creation corrected to `SbieIni.exe set <box> Enabled y`: probing with `Start.exe /Box:` never persisted anything (verified live); two `/wait` instances proven coexisting
- `param()`-after-statements parse bug in `bundle-setup.ps1`/`package-msix.ps1` (CLI args were unbindable)
- Backup of live DB failed on SQLite lock (`VACUUM INTO` snapshot); restore pool lock (`ClearAllPools`)
- `CredentialService.Save(null)` throws `ArgumentNullException` via `ThrowIfNullOrWhiteSpace`
- Issue template security URL pointed at the `ztemplate` template repo; now `zcamfrog`
- README test count synchronized (85)

### Security
- CodeQL `security-extended` for csharp, dependency-review enforced, DPAPI `CurrentUser` unchanged, process identity validation unchanged

## [1.0.0] - 2026-09-20
First tagged release. Published via the release workflow with `CamfrogMultiID-v1.0.0-win-x64.zip` + `.sha256` and build provenance attestation.
