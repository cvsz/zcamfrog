# Production Implementation Checklist

## Repository identity

- [x] Repository name and GitHub links use `cvsz/zcamfrog`.
- [x] Documentation describes Camfrog Multi-ID Manager rather than a generic repository.
- [x] Security contact points to this repository.
- [x] Project-specific build, test, release, and security instructions are present.

## Application

- [x] .NET 8 WPF target with nullable reference types and warnings-as-errors.
- [x] Single-instance startup and emergency diagnostics.
- [x] Add, start, stop, start-all, stop-all, and remove-account workflows.
- [x] Runtime reconciliation and bounded process termination.

## Persistence and credentials

- [x] SQLite initialization and forward-compatible migrations.
- [x] Case-insensitive username uniqueness.
- [x] WAL mode, busy timeout, and foreign-key enforcement.
- [x] DPAPI CurrentUser credential protection.
- [x] Temporary-file writes with failure cleanup.
- [x] Safe account deletion with credential/profile cleanup.

## Process safety

- [x] PID reuse detection.
- [x] Start-time validation.
- [x] Executable-path validation.
- [x] Fail-closed behavior when process identity cannot be verified.
- [x] Graceful close before bounded force termination.
- [x] No false "Stopped" state after failed termination.

## CI/security

- [x] Windows Release build.
- [x] Regression tests.
- [x] Self-contained win-x64 publish gate.
- [x] C# and GitHub Actions CodeQL.
- [x] Dependency review and Dependabot.
- [x] Static unfinished-marker scan.
- [x] Least-privilege workflow permissions.
- [x] Project-specific issue, PR, support, and security templates.

## Cross-build tooling

- [x] Ubuntu MinGW-w64 bootstrap script.
- [x] Standalone x86_64 MinGW CMake toolchain.
- [x] Headless Wine initialization for native Windows helper testing.
- [x] vcpkg bootstrap support.

## Manual production validation

- [ ] Test on a clean Windows installation.
- [ ] Test against the exact Camfrog client version intended for production.
- [ ] Verify the installed client actually honors separate profile directories.
- [ ] Verify recovery after manager restart and unexpected client exit.
- [ ] Verify no plaintext credentials appear in settings, logs, crash reports, or command lines.
- [ ] Verify signed release artifacts and checksum verification once release signing is enabled.
