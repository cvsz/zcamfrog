# AGENTS.md — Camfrog Multi-ID Manager Agent Contract

## Purpose
This repository is a Windows WPF desktop application (`.NET 8`,
`net8.0-windows`, `win-x64` self-contained), not a generic template.
Changes must keep the manager buildable, testable, and runnable on
Windows x64, preserve DPAPI/process-safety invariants, and stay honest
about what the third-party Camfrog client can and cannot do.

## Project layout
- `src/CamfrogMultiID.Core/` — domain models only, no UI/OS dependencies.
- `src/CamfrogMultiID.Infrastructure/` — SQLite, DPAPI, settings,
  process lifecycle, Sandboxie helpers.
- `src/CamfrogMultiID.App/` — WPF windows and startup path.
- `tests/CamfrogMultiID.Tests/` — xUnit (`net8.0-windows`).
- `.github/workflows/` — `ci.yml` (build matrix), `codeql.yml`
  (csharp+actions), `dependency-review.yml`, `release.yml` (tag releases).
- `docs/` — architecture, development, release, troubleshooting.

## Operating rules
- Read README.md, `docs/architecture.md`, SECURITY.md, ROADMAP.md, and
  CHANGELOG.md before editing.
- Keep code, configuration, filenames, commit messages, and technical
  documentation in English.
- Prefer the smallest reviewable change that satisfies the requested scope.
- Never weaken CI, security scanning, dependency review, or release
  controls merely to make a check pass.
- Never commit credentials, tokens, private keys, production endpoints,
  personal data, or realistic secrets. Never log or persist plaintext
  passwords; DPAPI `CurrentUser` only.
- Treat external input (room URLs, exe paths, templates, PRs from forks,
  dependency metadata) as untrusted: validate scheme (`camfrog:` only
  for room links), existence, and quoting.
- Reuse existing workflows and documents instead of creating overlapping
  alternatives. `Dockerfile` stays a Windows-only marker; MinGW/Wine
  tooling is auxiliary and never the production build path.
- Pin GitHub Actions to least privilege; prefer maintained
  first-party/verified actions (`checkout@v7`, `setup-dotnet@v6`).
- Sign commits with GPG. Do not rewrite pushed history except for an
  explicit resign sweep (then `--force-with-lease` only).
- This repo has no Linux/macOS support (WPF Windows-only) and no network
  service. Do not add either without explicit scope approval.

## Invariants (fail closed)
- Never terminate a process whose PID/start-time/executable identity does
  not match the tracked account.
- Stored secrets never enter logs, command lines, exception messages,
  or the database.
- A corrupt database row or failed refresh must degrade (log + keep old
  UI state), never crash the UI loop.
- XAML-wired change handlers must be inert during `InitializeComponent`
  (see `docs/troubleshooting.md` entry 7).

## Known pitfalls (evidence-backed, do not regress)
- `.sln` project GUIDs must be complete in `Build.0` lines; truncated
  GUIDs silently skip projects at restore (`MSB4121`/`NETSDK1004`).
  Verify with `dotnet restore -v diag | Select-String BuildProjectInSolution`.
- Ordinary restore does not cover the `win-x64` publish graph; always
  also run `dotnet restore src/.../CamfrogMultiID.App.csproj
  --force-evaluate -r win-x64` before publish.
- The running app locks its own single-file `publish\CamfrogMultiID.exe`;
  stop it before republishing.
- Test project is the only place `NoWarn` is acceptable
  (`CA1707;xUnit1031`); production projects stay warnings-as-errors.
- The CI marker scan must exclude documents that define the patterns
  (`CHANGELOG.md`, `AGENTS.md`, workflow self-references, `Makefile`).

## Camfrog client boundary (do not cross)
- No bypass of Camfrog auth/CAPTCHA/licensing; no credential injection;
  no "gold"/subscription generation; nickname colors come from paid
  status, not local code.
- The client is single-instance per Windows session; simultaneous
  instances require Sandboxie-Plus boxes (experimental integration
  exists — do not build a custom sandbox driver).
- Room auto-join uses the client's own `--url=` switch with validated
  `camfrog://join_room/?name=` links only.

## Change workflow
1. Inspect the current exact branch/head and existing files.
2. Reproduce or locate the failure with evidence (log, stack, command
   output) before changing code.
3. Add or update a regression test where practical.
4. Implement without widening scope.
5. Run `dotnet restore` (solution + win-x64 graph), Debug + Release
   builds, `dotnet test`, and `build-release.ps1`.
6. Update README/CHANGELOG/docs when behavior, setup, or release
   procedures change.
7. GPG-commit with a scope-prefixed message (`fix:`, `feat:`, `ci:`,
   `docs:`, `chore:`, `test:`); push; report exact-head evidence.

## Verification
At minimum: 0 warnings/0 errors, tests green with no skips unresolved,
publish artifact exists with checksum, no leaked secrets, no unfinished
production code, marker scan clean, and docs consistent with behavior.
Never claim production-ready from inspection alone.
