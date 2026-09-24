# Changelog

All notable changes to Camfrog Multi-ID Manager are documented here. Based on Keep a Changelog and Semantic Versioning.

## [Unreleased]
### Added
- Pre-start foreign-client warning: starting while an untracked copy of the client runs now asks first (single-instance handoff exits in ~1s otherwise); tray-minimized copies called out explicitly
- `FindForeignClientProcesses` (identity-verified, warning-only, never kills) + Thai/English strings

### Fixed
- Auto-restart loop guard never tripped: the restart budget was reset on every successful (re)start, so a client exiting in ~2s restarted forever. Budget now resets only on manual start/stop; automatic restarts consume it (3 per 10 min, then pause with Error)
- Issue template security URL pointed at the `ztemplate` template repo; now `zcamfrog`
- `IMPLEMENTATION-CHECKLIST.md` rewritten from generic template to project-specific gates
- README test count synchronized (79)

## [1.0.0] - 2026-09-20
First tagged release. Published via the release workflow with `CamfrogMultiID-v1.0.0-win-x64.zip` + `.sha256` and build provenance attestation.

## [Unreleased]
### Added
- Account lifecycle: Edit (display/username/enabled + optional password replace), Delete with confirmation + secret/profile cleanup, Enable/Disable toggle, Change Password dialog, Clear Error
- Main window: search/filter, selected-account details + launch preview, status bar (counts + client status), log level filter + tail 500 + auto-scroll + clear/open-folder, context menu, double-click edit, F5/Delete/Enter shortcuts, Start All / Stop All confirmations
- Settings: exe existence status, template warning validation, data folder display + open-folder
- Backend: `GetById`, `UpdateDetails`, `SetEnabled`, `Delete`, `ClearError`, `UsernameExistsExcept`, `CredentialService.Exists/Delete`, `PreviewCommandLine/PreviewArguments/ValidateArgumentsTemplate`
- Dependencies (verified `net8.0-windows`, 51/51 tests): `Microsoft.Data.Sqlite` 8.0.30→10.0.12, `ProtectedData` 8.0.0→10.0.12, `Test.Sdk` 17.8.0→18.10.1, `coverlet` 6.0.0→10.0.1, `xunit` 2.5.3→2.9.3, `runner.visualstudio` 2.5.3→4.0.0; Actions `checkout` v4→v7, `setup-dotnet` v4→v6, `upload-artifact` v4→v7
- Tests: `AccountManagementTests` (12 tests) — total 52 tests
- Startup crash fix: guard `MainWindow` event handlers during `InitializeComponent` (`_initialized`), corrupt `started_utc` now parses to null instead of throwing, refresh loop catches and logs instead of killing the UI
- Sandbox multi-instance (experimental): optional Sandboxie-Plus launch per account (`Start.exe /wait /Box:<user>`, box name sanitized); env-redirection alone proven insufficient (client mutex, PID 7364 test)
- Auto-join rooms: per-account `RoomUrl` (`camfrog:` scheme validated) appended as `--url=` (registry-verified client switch); launch preview shows full sandboxed command
- Sandboxie onboarding: status + one-click download + Create-Boxes-For-All in Settings, box state in details panel, `SandboxieService` (boxes root, exists, create), `bundle-setup.ps1/.cmd` (Sandboxie-Plus + manager bundle with checksum)
- Auto-restart (opt-in per account, `RestartPolicy`: 3 per 10 min, loop pauses to Error, silent timer path, budget reset on manual start/stop)
- Backup/restore in Settings (zip of DB snapshot + secrets + settings; traversal validation; `ClearAllPools` so live DB restores)
- Secrets-dir ACL restricted to current user; log export; screen-reader names; release provenance attestation
- Deps: `System.IO.FileSystem.AccessControl` 5.0.0 (latest stable; no 8.x exists)
- Health: uptime + restart-window counts in details; password-age with 90-day rotation notice (`password_changed_utc` migration)
- Scheduled auto-backups (configurable days/keep, prune, startup-safe) + diagnostics bundle export (no secrets/usernames, no network)
- `package-msix.ps1` path-ready (SDK-gated, unverified locally); release attestation bumped to v4 (Dependabot, merged)
- Localization: English + Thai (152 resx keys, hand-written accessor without designer dependency, `x:Static` XAML, `Language` setting, startup `CurrentUICulture`, `L10n.Fmt` helper satisfying CA1863/CA1304); real-process start/stop test via cmd fake client
- Sandboxie depth: `Start.exe` version detection in Settings, `-InstallSandboxie` elevation-gated silent install in bundle script, `docs/sandboxie.md` contract; fixed `param()`-after-statements parse bug in `bundle-setup.ps1`/`package-msix.ps1` (CLI args were unbindable)
- `tests/CamfrogMultiID.Tests` (xUnit, 39 tests): DatabaseService, CredentialService, SettingsService, ProcessSessionService (PID reuse, quoting, concurrency)
- CI `build` job on `windows-latest` (.NET 8): restore, Debug, Release, test, publish `win-x64` self-contained, artifact upload
- CodeQL matrix for `csharp` (windows) and `actions` (ubuntu), `setup-dotnet`, manual build
- Dependabot `nuget` ecosystem
- `AppPaths(string root)` injectable for test isolation
- Docs: `docs/architecture.md`, `docs/development.md`, `docs/release.md` synchronized to actual implementation; `docs/template-inventory.md` replaced with project inventory

### Changed
- `.gitignore` now excludes .NET artifacts (`bin/`, `obj/`, `.vs/`, `TestResults/`, `publish/`, `coverage/`, secrets)
- `Makefile` targets now run real `dotnet` commands (setup, format, lint, test, build, publish)
- `Dockerfile` documents Windows-only WPF (not supported in Linux container)
- `.github/workflows/ci.yml` now validates project files and runs full Windows build matrix
- `tests` added to solution; `CamfrogMultiID.sln` includes `tests/CamfrogMultiID.Tests`

### Fixed
- `CredentialService.Save(null)` now correctly throws `ArgumentNullException` via `ThrowIfNullOrWhiteSpace` (test expects `ArgumentException` family)
- Test `Quote_And_ExpandArguments_EscapesCorrectly` tolerance for profile path containing "profiles"

### Security
- CodeQL `security-extended` for csharp, dependency-review enforced, DPAPI `CurrentUser` unchanged, process identity validation unchanged

## [1.0.0] - 2026-09-20
### Added
- Initial Camfrog Multi-ID Manager baseline: WPF `net8.0-windows`, SQLite, DPAPI, process isolation, single-instance, `build-release.ps1`

## [Template] - 2026-08-21
- Repository template baseline (ztemplate)
