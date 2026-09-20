# Repository Engineering Inventory

This document records the engineering and GitHub controls that are actually enabled for Camfrog Multi-ID Manager.

## Project documentation

- README.md — user-facing build, runtime, troubleshooting, and production validation guide.
- ABOUT.md — project scope and engineering focus.
- AGENTS.md — repository automation/engineering contract.
- CONTRIBUTING.md — contribution and validation workflow.
- SECURITY.md — vulnerability reporting and security requirements.
- GOVERNANCE.md — project decision and maintenance model.
- CODE_OF_CONDUCT.md — community standards.
- CHANGELOG.md — release history.
- ROADMAP.md — completed work and planned improvements.
- IMPLEMENTATION-CHECKLIST.md — production readiness tracking.

## GitHub community and ownership

- .github/CODEOWNERS
- .github/PULL_REQUEST_TEMPLATE.md
- .github/SUPPORT.md
- .github/ISSUE_TEMPLATE/bug_report.yml
- .github/ISSUE_TEMPLATE/feature_request.yml
- .github/ISSUE_TEMPLATE/security.yml
- .github/ISSUE_TEMPLATE/config.yml
- .github/release.yml

## CI and security automation

- .github/workflows/ci.yml — Windows build/test/publish plus repository checks.
- .github/workflows/codeql.yml — C# and GitHub Actions analysis.
- .github/workflows/dependency-review.yml — pull-request dependency review.
- .github/workflows/release.yml — tagged Windows release packaging.
- .github/dependabot.yml — GitHub Actions and NuGet updates.
- .github/codeql-config.yml — extended CodeQL query configuration.

## Build and test tooling

- CamfrogMultiID.sln
- Directory.Build.props
- build-release.ps1
- build-release.cmd
- Makefile
- tests/CamfrogMultiID.Tests
- scripts/bootstrap-ubuntu-mingw-wine-vcpkg.sh
- toolchain-x86_64-w64-mingw32.cmake

## Intentionally absent

- Dockerfile: not applicable to the supported WPF desktop runtime.
- Container deployment manifests: not applicable to the current application architecture.
- Funding configuration: no project funding program is currently enabled.

## Validation principle

The inventory must describe files and workflows that exist in this repository. Generic template placeholders and stale adoption instructions should not remain in project documentation.
