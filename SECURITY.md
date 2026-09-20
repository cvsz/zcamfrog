# Security Policy

## Supported versions

| Version | Support |
| --- | --- |
| Current `main` | Security fixes and dependency updates |
| Released versions | Best effort; upgrade to the latest release when practical |

## Reporting a vulnerability

Do not disclose exploitable vulnerabilities in public issues, pull requests, discussions, or commit messages. Use GitHub private vulnerability reporting/security advisories when enabled for this repository.

Include affected versions or commits, reproduction details, impact, prerequisites, and suggested remediation when available.

## Security expectations

- Never commit credentials, tokens, private keys, production secrets, or sensitive personal data.
- Treat executable paths, argument templates, profile paths, and imported data as untrusted configuration.
- Keep process termination fail-closed: a PID must not be terminated unless its tracked start time and executable identity can be verified.
- Keep passwords outside SQLite and protect them with Windows DPAPI `CurrentUser`.
- Never put passwords into command-line arguments, logs, settings, or crash reports.
- Keep dependencies patched and review Dependabot alerts.
- Keep CodeQL and dependency-review workflows enabled.
- Use least-privilege GitHub Actions permissions.
- Do not weaken security gates merely to obtain a passing build.

## Incident handling

For a confirmed security issue:

1. Stop affected account-management operations if necessary.
2. Preserve sanitized logs and the affected version/commit.
3. Remediate and add regression coverage.
4. Validate with clean build, tests, and security workflows.
5. Publish release notes without exposing exploit details.

## Project-specific security boundary

Camfrog Multi-ID Manager is a local process manager. It does not bypass authentication, CAPTCHA, licensing, rate limits, or other Camfrog access controls, and stored passwords are not automatically submitted to the client.
