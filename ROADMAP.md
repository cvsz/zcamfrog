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
- [x] Process: auto-restart on crash (opt-in, 3-per-10-min loop guard), health via details + status bar
- [x] Persistence: backup/restore of `camfrog.db` and `secrets/` (traversal-safe zip, `VACUUM INTO` snapshot)
- [x] Security: secrets-dir ACL (current user only), audit via event log + log export
- [x] Accessibility: screen-reader names on main controls, full keyboard operation
- [x] Provenance: `attest-build-provenance` on release zips (verifies on first tag release)
- [x] Diagnostics: opt-in local bundle (versions, counts, log, settings; no secrets/usernames, no network)
- [x] Health: uptime + restart counts surfaced; password-age rotation notice
- [x] Scheduled backups with pruning
- [~] Installer: `package-msix.ps1` path-ready (needs Windows SDK + cert; unverified here, zip stays primary)
- [ ] Telemetry: networked opt-in diagnostics (no discretionary need while local bundle exists)
- [ ] Localization (resources)

## Future considerations
- [ ] Support for Camfrog client variants (if documented CLI changes)
- [ ] Localization (resources)
- [ ] Accessibility audit and keyboard navigation improvements
- [ ] Signed artifacts and provenance (Sigstore)

## Non-goals
- No bypass of Camfrog auth/CAPTCHA/licensing.
- No network service, no cloud sync.
- No Linux/macOS support (WPF Windows-only).
