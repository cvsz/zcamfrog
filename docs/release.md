# Release

## Versioning

Use Semantic Versioning for tagged releases such as `v1.0.0`.

## Pre-release checklist

1. Ensure the PR is merged to `main`.
2. Ensure Windows CI build and tests pass.
3. Ensure CodeQL and dependency review pass.
4. Review `CHANGELOG.md`.
5. Run `.\build-release.ps1` on a clean Windows environment.
6. Verify `CamfrogMultiID.exe` exists in the self-contained x64 publish directory.
7. Test first-run settings, account creation, start/stop, reconciliation, and removal.
8. Confirm logs contain no plaintext passwords.
9. Confirm the exact Camfrog client version intended for the release has been manually validated.

## Automated tagged release

Push a version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions `.github/workflows/release.yml` then:

1. Checks out the tagged commit.
2. Installs .NET 8.
3. Restores and builds the solution.
4. Runs regression tests.
5. Executes `build-release.ps1`.
6. Packages the self-contained publish directory as `CamfrogMultiID-vX.Y.Z-win-x64.zip`.
7. Generates a SHA-256 checksum.
8. Creates the GitHub Release and uploads the ZIP and checksum.

The release workflow must use the exact tag commit; it must never package uncommitted workspace state.

## Rollback

Keep the previous known-good release artifact. If a release is defective, stop using the affected binary, restore the previous release, and preserve diagnostics for remediation. Database migrations should not be manually reverted without a tested migration plan.
