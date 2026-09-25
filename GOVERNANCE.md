# Governance — Camfrog Multi-ID Manager

## Maintainer
[@cvsz](https://github.com/cvsz) — reviews changes, protects quality and
security, manages releases and tags.

## Decisions
- Small, reviewable changes on `main` (feature branches for larger work).
- Material architecture/security/release decisions recorded as ADRs under
  `docs/adr/` or in `CHANGELOG.md`.
- Security-sensitive changes require extra review and must fail closed.

## Change rules
- Follow `AGENTS.md` (agent contract) and `CONTRIBUTING.md` (workflow).
- GPG-sign commits. Do not rewrite pushed history except an explicitly
  approved resign sweep (`--force-with-lease` only).
- CI (`CI`, `CodeQL`, `Dependency Review`) must be green; `release.yml`
  tags (`v*.*.*`) produce signed-release artifacts with provenance.

## Support
See `.github/SUPPORT.md`. Security reports go through `SECURITY.md`
channels, never public issues.
