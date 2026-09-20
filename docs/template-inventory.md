# Repository Template Inventory

This repository provides a secure, reusable baseline for new GitHub projects.

## Governance and community
- AGENTS.md
- README.md
- ABOUT.md
- CONTRIBUTING.md
- CODE_OF_CONDUCT.md
- GOVERNANCE.md
- SECURITY.md
- .github/SUPPORT.md
- issue forms and pull request template
- CODEOWNERS

## Automation and security
- baseline CI
- CodeQL and CodeQL configuration
- dependency review
- Dependabot
- release notes configuration
- least-privilege workflow guidance

## Engineering lifecycle
- CHANGELOG.md
- ROADMAP.md
- IMPLEMENTATION-CHECKLIST.md
- architecture, development, release, and ADR documentation
- Dockerfile, Makefile, environment example, EditorConfig, Git attributes, and Git ignore baseline

## Adoption checklist
After creating a repository from this template, replace project placeholders, review CODEOWNERS and security contacts, select the actual language/runtime CI matrix, configure required branch/ruleset checks, configure only required secrets/environments, and remove optional files that the project intentionally does not use.

Never copy production credentials into a generated repository.
