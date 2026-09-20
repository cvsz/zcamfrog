# Contributing to Camfrog Multi-ID Manager

Thanks for contributing to Camfrog Multi-ID Manager.

## Development workflow

1. Create a focused branch from `main`.
2. Read `AGENTS.md`, `README.md`, and the relevant `docs/` guidance.
3. Add or update regression tests for behavior changes.
4. Run restore, Release build, tests, formatting, linting, and security checks.
5. Update project documentation and `CHANGELOG.md` when behavior or user-visible output changes.
6. Open a pull request and complete the project PR checklist.

## Branch naming

Use concise prefixes such as `feat/`, `fix/`, `docs/`, `chore/`, `refactor/`, `test/`, or `security/`.

## Commit guidance

Prefer Conventional Commits, for example:

- `feat: add account editing`
- `fix: prevent PID reuse termination`
- `security: harden credential cleanup`
- `docs: update release procedure`

## Required validation

For application changes:

```powershell
dotnet restore .\CamfrogMultiID.sln
dotnet build .\CamfrogMultiID.sln -c Release --no-restore
dotnet test .\tests\CamfrogMultiID.Tests\CamfrogMultiID.Tests.csproj -c Release --no-restore
dotnet format .\CamfrogMultiID.sln --verify-no-changes --no-restore
```

For a production artifact:

```powershell
.\build-release.ps1
```

Do not bypass warnings-as-errors, CodeQL, dependency review, or security gates to make a pull request green.

## Security

Do not report exploitable vulnerabilities in public issues. Follow `SECURITY.md`.
