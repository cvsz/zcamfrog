# Camfrog Multi-ID Manager

Windows WPF desktop application (`.NET 8`, `net8.0-windows`, `win-x64`
self-contained) for managing multiple local Camfrog client identities:
isolated profiles, DPAPI-protected passwords, fail-closed process
lifecycle, optional Sandboxie-Plus multi-instance support, and
`camfrog:` room auto-join. English + Thai UI.

## Security boundary

This application launches the Camfrog client you installed. It does
**not** bypass authentication, CAPTCHA, rate limits, or licensing; it
does not inject credentials, generate subscriptions, or change nickname
colors. Stored passwords use Windows DPAPI (`CurrentUser`) and are
never logged, placed on command lines, or stored in the database.

## Requirements

- Windows 10/11 x64, `.NET 8 SDK` (`8.0.425+` known good).
- No admin rights for daily use (admin needed only to install
  Sandboxie-Plus or create Sandboxie boxes).

## Build

```powershell
dotnet --info
dotnet restore .\CamfrogMultiID.sln
dotnet restore .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj --force-evaluate -r win-x64
dotnet build .\CamfrogMultiID.sln -c Debug --no-restore
dotnet build .\CamfrogMultiID.sln -c Release --no-restore
dotnet test .\CamfrogMultiID.sln -c Release --no-build
```

Test results are reported by the runner and CI; this file intentionally
states no hardcoded test count (see `.github/workflows/ci.yml`).

Run (development):

```powershell
dotnet run --project .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj -c Release
```

## Production publish

```powershell
.\build-release.ps1
```

Output:

```text
src\CamfrogMultiID.App\bin\Release\net8.0-windows\win-x64\publish\CamfrogMultiID.exe
```

Self-contained single-file `win-x64`. If PowerShell blocks the unsigned
script, use `build-release.cmd` (single-process `-ExecutionPolicy
Bypass`, no machine change).

Tagged releases (`v*.*.*`) additionally produce a versioned zip +
`.sha256` and build provenance via `.github/workflows/release.yml`.

## Runtime data

Under `%LOCALAPPDATA%\CamfrogMultiID\`:

- `camfrog.db` — accounts, runtime state, event log (SQLite, WAL-friendly,
  case-insensitive username uniqueness)
- `settings.json` — client exe, argument template, Sandboxie, backup,
  language options
- `profiles\` — per-account profile directories
- `secrets\` — DPAPI blobs, directory ACL restricted to current user
- `logs\app.log` — operational log (rotates ~5 MiB), `startup-error.log`
- `backups\` — scheduled/manual backup zips (DB snapshot + secrets +
  settings; profiles excluded)

## First run

1. Start `CamfrogMultiID.exe`.
2. Settings → select the installed Camfrog executable.
3. Add an account (username, password, optional `camfrog://join_room`
   link, auto-restart opt-in).
4. Start Selected → confirm the client behaves.
5. For simultaneous instances: install Sandboxie-Plus, enable it in
   Settings, Create Boxes, Start All. See `docs/sandboxie.md`.

## Features

- Account lifecycle: add/edit/delete, enable/disable, change password,
  clear error, duplicate detection (case-insensitive).
- Process safety: PID/start-time/executable validation, stale-PID
  refusal, graceful close → bounded wait → tree kill, Sandboxie box
  `/terminate` on stop.
- Auto-restart (opt-in, 3 per 10 min, then pauses with Error).
- Search/filter, per-account details + launch preview, health dashboard
  (presence, room, uptime, restarts, 30-event feed), status bar.
- Log viewer (level filter, export, diagnostics bundle without secrets).
- Backup/restore (traversal-safe) + scheduled auto-backups with pruning.
- Password-age tracking with 90-day rotation notice.

## Limitations (honest)

- The client is single-instance per Windows session; concurrent
  instances need Sandboxie-Plus boxes (experimental, needs its
  driver/service installed as admin).
- Room auto-join passes the client's own `--url=` switch; server-side
  room membership is not observable — the UI reports "auto-join sent",
  never "joined".
- A stored password is never auto-submitted to the client.
- If the client ignores `{profile}`, instances may share client state.

## Troubleshooting

See `docs/troubleshooting.md` (captured failure modes: restore gaps,
solution GUID corruption, startup handler ordering, publish file locks,
script `param()` placement). Diagnostics: main window → Diagnostics —
exports versions, counts, log, settings (no secrets/usernames).

## History

Past per-version changes live in `CHANGELOG.md`. This README describes
the current tree only.
