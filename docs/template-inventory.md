# Project Inventory — Camfrog Multi-ID Manager

This document replaces the generic template inventory and reflects the current repository state.

## Solution

- `CamfrogMultiID.sln` (projects: Core, Infrastructure, App, Tests)

## Source

- `src/CamfrogMultiID.Core/` — domain models (`CamfrogAccount`, `AppSettings`)
- `src/CamfrogMultiID.Infrastructure/` — `AppPaths`, `DatabaseService`, `CredentialService`, `SettingsService`, `ProcessSessionService` (SQLite + DPAPI + Process)
- `src/CamfrogMultiID.App/` — WPF (`App.xaml`, `MainWindow`, `AccountWindow`, `SettingsWindow`, `app.manifest`)
- `tests/CamfrogMultiID.Tests/` — xUnit coverage for all layers

## Build and release

- `build-release.ps1` / `build-release.cmd` — deterministic restore → build → publish `win-x64` self-contained
- `Directory.Build.props`, `.editorconfig`, `Makefile` (dotnet targets)

## CI/CD

- `.github/workflows/ci.yml` — windows-latest .NET 8 build matrix (restore, Debug, Release, test, publish) + ubuntu baseline
- `.github/workflows/codeql.yml` — CodeQL for `csharp` and `actions` (security-extended)
- `.github/workflows/dependency-review.yml` — PR dependency review
- `.github/dependabot.yml` — `github-actions` + `nuget` + `docker`
- `.github/codeql-config.yml` — security-extended queries

## Governance and docs

- `README.md` — production build baseline, security boundary, publish procedure
- `ABOUT.md`, `AGENTS.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `GOVERNANCE.md`, `SECURITY.md`
- `CHANGELOG.md`, `ROADMAP.md`, `IMPLEMENTATION-CHECKLIST.md`
- `docs/architecture.md`, `docs/development.md`, `docs/release.md`, `docs/adr/`

## Configuration

- `.gitignore` — .NET artifacts (`bin/`, `obj/`, `.vs/`, `TestResults/`, `publish/`) + secrets
- `.gitattributes` — `text=auto eol=lf`, binaries
- `Dockerfile` — documents Windows-only (not supported in Linux container)

## Scripts

- `run-diagnostic.cmd` — verifies publish and process liveness
