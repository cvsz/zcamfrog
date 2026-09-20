# Roadmap — Camfrog Multi-ID Manager

## Current (v1.0)
- [x] WPF `net8.0-windows` `win-x64` self-contained, single-file
- [x] SQLite with case-insensitive username uniqueness, DPAPI secrets, atomic writes
- [x] Process isolation with PID/start-time/executable validation, fail-closed reuse protection
- [x] Single-instance mutex, startup diagnostics, reconciler, log rotation
- [x] Build: `build-release.ps1` deterministic, CI on `windows-latest`, CodeQL `csharp`, 39 tests
- [x] Docs: architecture, development, release synchronized

## Next
- [x] UI: edit/delete account, enable/disable, search/filter
- [x] UI: per-account launch arguments preview and validation
- [x] UI: selected-account details, status bar, log filter, context menu + shortcuts, change password, clear error
- [ ] Process: auto-restart on crash (opt-in), health dashboard
- [ ] Persistence: backup/restore of `camfrog.db` and `secrets/`
- [ ] Security: additional hardening (ACL on secrets, audit log)
- [ ] Installer: MSIX / WiX bundle (optional)
- [ ] Telemetry: opt-in anonymized diagnostics (no secrets)

## Future considerations
- [ ] Support for Camfrog client variants (if documented CLI changes)
- [ ] Localization (resources)
- [ ] Accessibility audit and keyboard navigation improvements
- [ ] Signed artifacts and provenance (Sigstore)

## Non-goals
- No bypass of Camfrog auth/CAPTCHA/licensing.
- No network service, no cloud sync.
- No Linux/macOS support (WPF Windows-only).
