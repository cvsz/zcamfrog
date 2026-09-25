# third_party — Sandboxie reference

This directory holds **local-only reference material** for the
Sandboxie integration. It is intentionally **not committed** (see
`.gitignore`): Sandboxie is GPL-3.0, and this repository must not
vendor its source. The manager integrates by launching the installed
`Start.exe` as a separate process — no Sandboxie code is embedded,
linked, or modified.

## Upstream
- Repo: `https://github.com/sandboxie-plus/Sandboxie`
- Reviewed release: v1.18.4 (2026-09-24, via GitHub API)
- Reference file: `Sandboxie/ref/start.cpp` (fetched 2026-09-24 from
  `master`), used to verify the `Start.exe` command-line contract.

## Re-fetch (sparse, no full clone needed)
```powershell
# Full history is large; fetch only what is needed, e.g.:
Invoke-WebRequest `
  -Uri 'https://raw.githubusercontent.com/sandboxie-plus/Sandboxie/master/Sandboxie/apps/start/start.cpp' `
  -OutFile 'third_party/Sandboxie/ref/start.cpp'
```

## Verified contract facts (from `start.cpp`)
- `Validate_Box_Name` reports Sbie message 3204
  ("Invalid box name parameter") when `SbieApi_IsBoxEnabled`
  fails — i.e. the box is unknown/disabled, not (only) badly formed.
- `Parse_Command_Line` accepts box-name characters `[alphanumeric + _]`
  and stops at anything else; a leading `"` resets the name to empty,
  which also yields 3204. **Box names must be passed unquoted.**
- A missing SbieSvc driver/service fails earlier with message 2331,
  which is why the manager checks service readiness first.
