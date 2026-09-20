# Architecture

## System context

Camfrog Multi-ID Manager is a Windows WPF desktop application that launches locally installed Camfrog client processes. It does not implement Camfrog authentication or bypass CAPTCHA, licensing, rate limits, or access controls.

## Components

- **App** — WPF startup, single-instance enforcement, dependency initialization, and global exception handling.
- **Core** — account and settings models.
- **Infrastructure** — SQLite persistence, DPAPI credential storage, settings persistence, process lifecycle and identity validation.
- **UI** — account management, settings, process controls, runtime reconciliation, and log viewer.

## Persistence

Data is stored below `%LOCALAPPDATA%\\CamfrogMultiID`:

- `camfrog.db` — account/runtime/event metadata.
- `settings.json` — client executable and documented argument template.
- `profiles\\` — per-account profile directories.
- `secrets\\` — DPAPI-protected credential blobs.
- `logs\\` — operational logs with bounded rotation.

SQLite uses WAL mode, a busy timeout, foreign-key enforcement, and case-insensitive username uniqueness.

## Process lifecycle and trust boundary

The configured executable path is resolved to a full local path before launch. A managed process is identified by PID plus recorded start time and executable path.

Termination is fail-closed: if start time or executable identity cannot be verified, the manager refuses to kill the PID. Windows exposes process start time and main-module information through APIs that can throw when process state or access is unavailable, so treating an inspection failure as a match would be unsafe. citeturn2search0turn2search3

## Credential security

Passwords are encrypted with Windows DPAPI using `DataProtectionScope.CurrentUser` and are never written to SQLite. Temporary secret files are deleted after replacement or failure.

## Deployment

The supported runtime is Windows x64 with .NET 8 or a newer SDK capable of targeting `net8.0-windows`. Production artifacts are self-contained single-file x64 publishes.

## Observability

Application events are written to SQLite and a text log. The text log is rotated at approximately 5 MiB. Startup failures also have an emergency log path.

## Known constraints

- Camfrog behavior around multiple concurrent local instances and profile isolation depends on the installed Camfrog client.
- The manager does not automatically submit stored passwords to Camfrog.
- The argument template is limited to `{username}` and `{profile}` substitutions.
