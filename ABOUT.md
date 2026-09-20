# About Camfrog Multi-ID Manager

Camfrog Multi-ID Manager is a Windows desktop utility for managing multiple local Camfrog client instances with isolated application profiles, tracked process identity, SQLite runtime metadata, and Windows DPAPI-protected credentials.

## Project focus

- Windows WPF desktop application
- .NET 8 / Windows x64
- Safe local process lifecycle management
- Per-account profile isolation
- SQLite persistence
- Windows DPAPI CurrentUser credential protection
- Deterministic self-contained production publishing
- Automated CI, tests, CodeQL, dependency review, and dependency maintenance

## Security boundary

The application launches the locally installed Camfrog client. It does not bypass Camfrog authentication, CAPTCHA, licensing, rate limits, or other access controls, and it does not inject stored passwords into the client.

## Engineering principles

This repository treats build, test, security, documentation, and operational readiness as part of the product. Security-sensitive behavior is fail-closed, and CI gates must be fixed rather than bypassed.

## Supported environment

- Windows 10/11 x64
- .NET 8 SDK or a compatible newer SDK
- Installed Camfrog client for runtime validation
- PowerShell 7 recommended for release automation

## Repository

GitHub: https://github.com/cvsz/zcamfrog
Owner: @cvsz
