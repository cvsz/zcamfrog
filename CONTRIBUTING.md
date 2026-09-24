# Contributing — Camfrog Multi-ID Manager

Thanks for contributing. This is a Windows WPF `.NET 8` project; you need
the .NET 8 SDK on Windows x64.

## Development workflow

1. Fork or create a feature branch from `main`.
2. Keep changes focused and reviewable.
3. Add or update tests for behavior changes.
4. Run, in order (see `docs/development.md`):
   `dotnet restore` (solution + win-x64 graph), Debug + Release builds
   (0 warnings), `dotnet test`, `build-release.ps1`.
5. Update documentation and `CHANGELOG.md` when relevant.
6. Open a pull request and complete the checklist.

## Branch naming

Use concise prefixes such as `feat/`, `fix/`, `docs/`, `chore/`, `refactor/`, `test/`, or `security/`.

## Commit guidance

Prefer Conventional Commits, for example:

- `feat: add project scaffolding`
- `fix: handle empty configuration`
- `security: harden token validation`
- `docs: update deployment guide`

## Pull requests

Pull requests should explain the problem, implementation, testing, security impact, compatibility impact, and rollback plan where applicable. Do not bypass quality or security checks to make a pull request green.

## Security

Do not report exploitable vulnerabilities in public issues. Follow `SECURITY.md`.
