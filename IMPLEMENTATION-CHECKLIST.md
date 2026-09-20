# Implementation Checklist

Use this checklist after creating a repository from `ztemplate`.

## Repository identity

- [ ] Replace `ztemplate` references with the real project name.
- [ ] Replace template descriptions and badges.
- [ ] Confirm license ownership and year.
- [ ] Configure repository topics, description, homepage, and template status.

## Ownership and governance

- [ ] Update `.github/CODEOWNERS`.
- [ ] Review `CONTRIBUTING.md` and `CODE_OF_CONDUCT.md`.
- [ ] Configure branch protection or repository rulesets.
- [ ] Require pull request review where appropriate.
- [ ] Require passing status checks before merge.

## Security

- [ ] Review `SECURITY.md` and configure private vulnerability reporting.
- [ ] Enable Dependabot alerts and security updates.
- [ ] Review CodeQL language detection/support for the actual stack.
- [ ] Keep dependency review enabled for pull requests where supported.
- [ ] Configure secret scanning and push protection where available.
- [ ] Add stack-specific SAST, container, IaC, and SBOM checks as needed.
- [ ] Confirm Actions permissions follow least privilege.

## Development

- [ ] Select the language/runtime and package manager.
- [ ] Add formatter and linter configuration.
- [ ] Add unit, integration, and end-to-end tests as appropriate.
- [ ] Replace placeholder Makefile targets with real commands.
- [ ] Replace or remove the placeholder Dockerfile.
- [ ] Populate `.env.example` with safe non-secret keys only.

## CI/CD

- [ ] Customize CI for the selected stack.
- [ ] Pin runtime versions and define supported-version matrices.
- [ ] Add build and package validation.
- [ ] Add artifact retention settings where needed.
- [ ] Configure environments, approvals, and deployment protections.
- [ ] Verify workflows from forks do not receive unsafe credentials.

## Release

- [ ] Decide on Semantic Versioning or another explicit versioning policy.
- [ ] Configure changelog and release-note generation.
- [ ] Configure package/container publishing only when needed.
- [ ] Add provenance, signing, and attestations for production artifacts where appropriate.
- [ ] Document rollback procedures.

## Documentation

- [ ] Complete `docs/architecture.md`.
- [ ] Complete `docs/development.md`.
- [ ] Complete `docs/release.md`.
- [ ] Add ADRs for material architectural decisions.
- [ ] Document operational ownership and support expectations.

## Final verification

- [ ] Fresh clone works with documented bootstrap steps.
- [ ] CI passes on `main` and pull requests.
- [ ] No secrets or private information are committed.
- [ ] Security checks are enabled and passing.
- [ ] A release can be created and rolled back according to documentation.
