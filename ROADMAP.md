# Roadmap — Camfrog Multi-ID Manager

Priorities: P0 security/correctness, P1 release blockers, P2
reliability, P3 UX, P4 future. Evidence = commit + tests + CI run on
`main` at time of writing (`d4b979f`; verify current HEAD).

## P0 — done (evidence: 94/94 tests, 0 warnings, CI + CodeQL green)

- [x] Fail-closed process identity (PID/start-time/executable, stale-PID refusal)
- [x] DPAPI `CurrentUser` secrets; no plaintext in logs/CLI/DB
- [x] Case-insensitive username uniqueness; parameterized SQLite
- [x] Traversal-safe backup/restore; `VACUUM INTO` live snapshots
- [x] `camfrog:`-only room URLs, validated at UI and DB boundary
- [x] Startup-crash class eliminated (`_initialized` guards + refresh catch)

## P1 — done

- [x] Solution GUID integrity; Debug + Release matrix; win-x64 publish
- [x] Tagged releases with zip + `.sha256` + provenance (`v1.0.0` shipped)
- [x] No CI fallback paths; CodeQL + dependency review enforced

## P2 — done

- [x] Auto-restart with loop guard (3/10 min, silent timer path)
- [x] Secrets-dir ACL + audit log + log export + diagnostics bundle
- [x] Scheduled auto-backups with pruning; password-age notice

## P3 — done

- [x] Full account lifecycle UI; search; details + launch preview
- [x] Health dashboard (presence, room, uptime, restarts, event feed)
- [x] English + Thai (key-parity tested); keyboard operation; screen-reader names

## Experimental (needs external proof per client/driver version)

- [~] Sandboxie multi-instance (works on this machine; re-verify after client updates)
- [~] MSIX packaging (`package-msix.ps1`, needs SDK + cert)

## P4 — future (not started, no claims made)

- [ ] Networked opt-in telemetry (design only: `docs/telemetry.md`)
- [ ] MSIX signing/distribution

## Non-goals

- No bypass of Camfrog auth/CAPTCHA/licensing; no credential injection.
- No network service; no cloud sync; no Linux/macOS support.
