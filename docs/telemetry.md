# Telemetry — Proposal (not implemented)

Status: **design only**. No automated collection, no network calls, and no
telemetry code exist in the manager today. The opt-in local diagnostics
bundle (Main window → Diagnostics...) already covers support cases.
Implement the below only after explicit scope approval.

## Principles (non-negotiable)
1. Opt-in, default off, reversible in Settings with one click.
2. Local-first: events append to a local SQLite `telemetry` table only.
3. No network transmission in v1. Sharing = the existing manual
   diagnostics export (user inspects the zip before sending it).
4. Never record: passwords, DPAPI blobs, credential file contents,
   usernames, display names, room URLs, profile paths, exe paths,
   machine name, user name, IP addresses.
5. Aggregates only: counts, durations, version strings, feature flags.

## Proposed event set
| Event | Fields |
|---|---|
| `app.start` | app version, OS version, language |
| `app.stop` | session duration, clean shutdown flag |
| `account.start` / `account.stop` | result (`ok`/`error`), duration bucket |
| `account.auto_restart` | attempt count in window, result |
| `backup.created` / `backup.restored` | result, byte-size bucket |
| `sandbox.used` | boolean only (no box names) |

Account references by row id hash (SHA-256 of `id` + per-install salt
stored beside settings), never by username.

## Storage and retention
- `telemetry(utc, event, fields_json)` in `camfrog.db`, 90-day rolling
  delete on startup. Export joins the diagnostics bundle only when the
  user clicks export.

## Open questions for approval
- Is even local-only collection desired, given the diagnostics bundle?
- Any future network endpoint must be documented here first, with sample
  payloads and a user-visible send log.
