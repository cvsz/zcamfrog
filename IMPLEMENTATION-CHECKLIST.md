# Implementation Checklist — Camfrog Multi-ID Manager

Use this checklist when changing the manager. All items are currently
satisfied on `main`; re-verify the touched areas per change.

## Build and test

- [x] `dotnet restore CamfrogMultiID.sln` + win-x64 graph restore succeed.
- [x] Debug and Release build with 0 warnings / 0 errors.
- [x] `dotnet test` green (79 tests, 0 skipped unresolved).
- [x] `build-release.ps1` publishes `CamfrogMultiID.exe` (checksum recorded).
- [x] Solution GUIDs complete (`BuildProjectInSolution=True` for all).

## Security

- [x] DPAPI `CurrentUser` only; no plaintext passwords in logs, CLI, DB.
- [x] Process stop refuses foreign PIDs (start-time + exe identity).
- [x] Room URLs restricted to the `camfrog:` scheme.
- [x] Backup/restore zips validated against path traversal.
- [x] Secrets directory ACL restricted to the current user.
- [x] CodeQL (`csharp`) and dependency review enabled; least privilege.
- [x] No secrets committed (see `docs/troubleshooting.md` entry 5).

## CI/CD

- [x] `ci.yml` builds/tests/publishes the actual WPF app on Windows.
- [x] `release.yml` tags (`v*.*.*`) produce zip + `.sha256` + provenance.
- [x] Dependabot covers `github-actions` and `nuget`.
- [x] Proven release: `v1.0.0` workflow run green with artifacts.

## Documentation

- [x] `docs/architecture.md`, `development.md`, `release.md`,
  `troubleshooting.md`, `sandboxie.md` match behavior.
- [x] README build/run/publish sections current.
- [x] CHANGELOG updated per change; AGENTS.md project-specific.

## Release readiness

- [x] Fresh clone builds per `docs/development.md`.
- [x] Thai + English UI verified (build + clean launch + key coverage test).
- [x] Rollback = re-tag previous `vX.Y.Z` (see `docs/release.md`).
