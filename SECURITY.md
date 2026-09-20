# Security Policy

Security is part of the default delivery baseline for repositories created from this template.

## Reporting a vulnerability

Do not disclose exploitable vulnerabilities in public issues, pull requests, discussions, or commit messages. Use GitHub's private vulnerability reporting/security advisory capability when enabled for the repository, or contact the repository owner through an agreed private channel.

Include affected versions or commits, reproduction details, impact, prerequisites, and suggested remediation when available.

## Supported versions

Each generated project should replace this section with its real support policy before its first production release.

## Security expectations

- Keep dependencies patched and review Dependabot alerts.
- Keep CodeQL and dependency-review workflows enabled when supported.
- Use least-privilege GitHub Actions permissions.
- Never commit credentials, tokens, private keys, production secrets, or sensitive personal data.
- Validate untrusted input and enforce authorization at trust boundaries.
- Prefer fail-closed behavior for security-sensitive paths.
- Preserve tenant and data isolation where applicable.
- Review third-party actions and pin or constrain them according to project policy.
- Do not disable security gates merely to obtain a passing build.

## Incident handling

Projects generated from this template should document containment, remediation, validation, disclosure, and rollback procedures appropriate to their risk profile.
