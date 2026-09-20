# Changelog

All notable changes to Camfrog Multi-ID Manager are documented here.

The format follows Keep a Changelog and releases use Semantic Versioning.

## [Unreleased]

### Added

- Production baseline documentation for build, security, architecture, development, and release operations.
- Windows regression-test project covering infrastructure behavior.
- CodeQL analysis for C# and GitHub Actions.
- Dependency review and Dependabot automation.
- Ubuntu MinGW-w64, Wine, vcpkg, and CMake helper tooling for native Windows components.
- GitHub issue, support, security, PR, CODEOWNERS, and release configuration.

### Changed

- Hardened process lifecycle tracking with PID, process start time, and executable identity.
- Added fail-closed process termination.
- Added safe account removal with credential/profile cleanup.
- Enabled SQLite WAL, busy timeout, foreign keys, and case-insensitive username uniqueness.
- Standardized self-contained win-x64 production publishing.
- Removed the non-functional Docker placeholder because this is a Windows WPF desktop application.
- Synchronized repository documentation and GitHub automation with the actual project.

### Fixed

- Corrected the solution project configuration structure.
- Fixed analyzer and WPF compilation issues documented during the production hardening work.
- Fixed repository security scanning false positives caused by template/self-reference.
- Corrected the GitHub security contact URL to this repository.

### Security

- DPAPI CurrentUser protection remains the credential boundary.
- Process termination refuses to act when identity cannot be verified.
- GitHub Actions use least-privilege permissions.
- CodeQL and dependency review remain enabled.

