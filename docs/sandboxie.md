# Sandboxie Integration — Camfrog Multi-ID Manager

## Why
The Camfrog client enforces single-instance per Windows session
(verified: a second process with redirected `%APPDATA%` exits within
seconds; `CreateMutexW` is linked into the binary). Environment
sandboxing alone cannot run two accounts at once. Each account therefore
needs its own object namespace, which Sandboxie-Plus provides per box.

## What we use (and what we do not)
- Upstream: `https://github.com/sandboxie-plus/Sandboxie` (free,
  open-source, GPL-3.0). Latest reviewed: v1.18.4.
- The manager only launches Sandboxie's `Start.exe` as a separate
  process. No Sandboxie code is embedded, linked, or modified, so there
  is no license contamination of this repository.
- We deliberately do not write our own sandbox driver: kernel-level
  virtualization plus driver signing is out of scope (see README).

## Launch contract
Per account start with sandboxing enabled:

```text
Start.exe /wait /Box:<sanitized-username> "<client.exe>" <args> [--url="<room>"]
```

- `/wait` keeps `Start.exe` alive while the sandboxed client runs, so
  the tracked PID stays valid for PID/start-time/executable checks
  (which then apply to `Start.exe` itself).
- Box names are derived from the username (letters/digits, max 32
  chars, `account<id>` fallback).
- Boxes are created on demand via `Start.exe /Box:<name> cmd.exe /c exit`
  ("Create Boxes For All Accounts", or implicitly on first start).
- Default boxes root: `C:\Sandbox\<WindowsUser>\<Box>` (overridable in
  tests via `CAMFROGMULTIID_SANDBOX_ROOT`).

## Manager surfaces
- Settings: detection status (path, boxes root, `Start.exe` product
  version), download button, path override, box pre-creation.
- Details panel: box name + `[created]`/`[not created]` state.
- `bundle-setup.ps1 [-InstallSandboxie]`: downloads the Plus installer
  and this project's release, verifies checksums; silent install
  requires elevation.

## Limits
- Sandboxie must be installed separately (driver = admin).
- Multi-instance behavior ultimately depends on the client tolerating
  separate namespaces; verify per client version by starting two
  accounts and confirming both stay `Running`.
- Room auto-join (`--url=`) is orthogonal: it works with or without
  the sandbox wrapper.
