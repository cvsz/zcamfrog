# Changelog

All notable changes to Camfrog Multi-ID Manager are documented here. Based on Keep a Changelog and Semantic Versioning.

## [Unreleased]
### Added
- Account lifecycle: Edit (display/username/enabled + optional password replace), Delete with confirmation + secret/profile cleanup, Enable/Disable toggle, Change Password dialog, Clear Error
- Main window: search/filter, selected-account details + launch preview, status bar (counts + client status), log level filter + tail 500 + auto-scroll + clear/open-folder, context menu, double-click edit, F5/Delete/Enter shortcuts, Start All / Stop All confirmations
- Settings: exe existence status, template warning validation, data folder display + open-folder
- Backend: `GetById`, `UpdateDetails`, `SetEnabled`, `Delete`, `ClearError`, `UsernameExistsExcept`, `CredentialService.Exists/Delete`, `PreviewCommandLine/PreviewArguments/ValidateArgumentsTemplate`
- Dependencies (verified `net8.0-windows`, 51/51 tests): `Microsoft.Data.Sqlite` 8.0.30→10.0.12, `ProtectedData` 8.0.0→10.0.12, `Test.Sdk` 17.8.0→18.10.1, `coverlet` 6.0.0→10.0.1, `xunit` 2.5.3→2.9.3, `runner.visualstudio` 2.5.3→4.0.0; Actions `checkout` v4→v7, `setup-dotnet` v4→v6, `upload-artifact` v4→v7
- Tests: `AccountManagementTests` (12 tests) — total 51 tests
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
