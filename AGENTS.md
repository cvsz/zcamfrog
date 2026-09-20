# AGENTS.md — Camfrog Multi-ID Manager Repository Contract

## Purpose

This repository contains the Camfrog Multi-ID Manager Windows WPF application, its tests, build automation, security controls, and release tooling.

## Operating rules

- Read `README.md`, `CONTRIBUTING.md`, `SECURITY.md`, `ROADMAP.md`, and the closest `AGENTS.md` before editing.
- Keep code, configuration, filenames, commit messages, and technical documentation in English.
- Prefer the smallest reviewable change that satisfies the requested scope.
- Never weaken CI, CodeQL, dependency review, or release controls merely to make a check pass.
- Never commit credentials, tokens, private keys, production secrets, personal data, local databases, or Camfrog profile data.
- Treat executable paths, argument templates, profile paths, imported data, dependency metadata, and pull requests from forks as untrusted input.
- Preserve the project's Windows/.NET architecture unless a change explicitly adds a native helper or another supported component.
- Reuse existing workflows and documents instead of creating overlapping alternatives.
- Pin GitHub Actions permissions to least privilege and prefer maintained first-party/verified actions.

## Project-specific build rules

- Production application builds target `net8.0-windows` and `win-x64`.
- Normal development builds should not force a RuntimeIdentifier unless required by the command.
- Production publishing uses `build-release.ps1` and must produce `CamfrogMultiID.exe`.
- MinGW/CMake/Wine/vcpkg tooling is auxiliary infrastructure for native helper components and native tests; it does not replace the supported .NET WPF build.
- Process termination must remain fail-closed against PID reuse and executable identity mismatches.
- Passwords must remain protected by Windows DPAPI CurrentUser and must never be passed on command lines or written to logs.

## Change workflow

1. Inspect the exact branch/head and existing files.
2. Identify missing, inconsistent, or broken project behavior.
3. Add or update regression coverage where practical.
4. Implement the smallest complete change.
5. Run relevant build, test, formatting, lint, and security checks.
6. Update documentation when behavior, setup, security, or release procedures change.
7. Open/update the pull request and report exact validation evidence.

## Verification

At minimum verify touched Markdown/YAML syntax, workflow permissions/triggers, project links, absence of secret artifacts, and consistency between README, source behavior, tests, GitHub automation, and release documentation.

## Pull requests and releases

PRs must state scope, tests, security impact, compatibility/migration impact, documentation impact, and rollback considerations. Releases require green required checks and explicit artifact evidence.

## Security

Report vulnerabilities through `SECURITY.md`, not public issues. Security-sensitive changes must preserve fail-closed behavior.

## Documentation ownership

- `.github/`: GitHub automation, community health, ownership, issue/PR templates, release configuration.
- `docs/`: engineering, operations, release, and architecture guidance.
- Root Markdown files: repository-wide project policy and lifecycle guidance.
