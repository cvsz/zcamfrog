# AGENTS.md — Repository Agent Contract

## Purpose
This repository is a reusable GitHub project template. Changes must remain generic, secure by default, easy to customize, and safe to inherit into a newly generated repository.

## Operating rules
- Read README.md, CONTRIBUTING.md, SECURITY.md, ROADMAP.md, and the closest AGENTS.md before editing.
- Keep code, configuration, filenames, commit messages, and technical documentation in English.
- Prefer the smallest reviewable change that satisfies the requested scope.
- Never weaken CI, security scanning, dependency review, branch protections, or release controls merely to make a check pass.
- Never commit credentials, tokens, private keys, production endpoints, personal data, or realistic secrets. Use documented placeholders.
- Do not invent project-specific owners, domains, deployment providers, package registries, cloud accounts, or credentials.
- Preserve template portability across languages and frameworks unless a file explicitly declares a narrower scope.
- Reuse existing workflows and documents instead of creating overlapping alternatives.
- Pin permissions for GitHub Actions to least privilege and prefer maintained first-party/verified actions.
- Treat external input, generated artifacts, pull requests from forks, and dependency metadata as untrusted.

## Template placeholders
Use obvious placeholders such as `PROJECT_NAME`, `OWNER`, `example.com`, and `REPLACE_ME`. Any generated repository must be able to find and replace placeholders without exposing secrets.

## Change workflow
1. Inspect the current exact branch/head and existing files.
2. Identify the smallest missing or inconsistent template capability.
3. Add tests or validation first when practical.
4. Implement without widening scope.
5. Run the relevant validation and security checks.
6. Update documentation when behavior, setup, governance, or release procedures change.
7. Open a pull request; do not claim merge/release readiness without exact-head evidence.

## Verification
At minimum, verify Markdown/YAML syntax for touched files, workflow permissions/triggers, links and placeholders, absence of committed secrets, and consistency between README, templates, governance, security, and release documentation.

## Pull requests and releases
PRs must state scope, tests, security impact, compatibility/migration impact, documentation impact, deployment impact, and rollback. Releases require green required checks and explicit evidence; never infer production readiness from documentation alone.

## Security
Report vulnerabilities through SECURITY.md, not public issues. Security-related templates must redirect sensitive reports accordingly. Fail closed when a security-sensitive configuration is incomplete.

## Documentation ownership
- `.github/`: GitHub automation, community health, ownership, issue/PR templates.
- `docs/`: versioned engineering, operations, and release guidance.
- `docs/adr/`: architecture decision records.
- Root Markdown files: repository-wide policy and project lifecycle guidance.

## Nested AGENTS.md
Add a child AGENTS.md only when a subtree has durable rules that differ from this contract. The nearest AGENTS.md may add stricter local requirements but must not weaken repository-wide security rules.
