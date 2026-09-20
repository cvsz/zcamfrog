# Production Implementation Checklist

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

## Manual production validation
- [ ] Test on a clean Windows installation.
- [ ] Test against the exact Camfrog client version intended for production.
- [ ] Verify the installed client actually honors separate profile directories.
- [ ] Verify recovery after manager restart and unexpected client exit.
- [ ] Verify no plaintext credentials appear in settings, logs, crash reports, or command lines.
