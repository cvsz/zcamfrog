# Release

## Versioning

Use Semantic Versioning for tagged releases.

## Release checklist

1. Ensure Windows CI build and tests pass.
2. Ensure CodeQL and dependency review pass.
3. Update `CHANGELOG.md`.
4. Run `.\build-release.ps1` on a clean Windows environment.
5. Verify the self-contained x64 executable exists.
6. Test first-run settings, account creation, start/stop, reconciliation, and removal.
7. Confirm logs contain no plaintext passwords.
8. Tag and publish through the trusted GitHub workflow.

## Rollback

Keep the previous known-good release artifact. If a release is defective, stop using the affected binary, restore the previous release, and preserve diagnostics for remediation. Database migrations should not be manually reverted without a tested migration plan.
