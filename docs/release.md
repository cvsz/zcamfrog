# Release — Camfrog Multi-ID Manager

## Versioning
Semantic Versioning (`MAJOR.MINOR.PATCH`). Tag releases as `vX.Y.Z` (e.g., `v1.0.0`). Changelog in `CHANGELOG.md`.

## Pre-release checklist
1. `dotnet restore` (solution + `src/CamfrogMultiID.App --force-evaluate -r win-x64`)
2. `dotnet build CamfrogMultiID.sln -c Release --no-restore` (0 warnings, 0 errors)
3. `dotnet test CamfrogMultiID.sln -c Release --no-build` (39 tests pass)
4. `.\build-release.ps1` (publish `win-x64` self-contained, `PublishSingleFile=true`)
5. Verify publish: `src/CamfrogMultiID.App/bin/Release/net8.0-windows/win-x64/publish/CamfrogMultiID.exe` exists; size reasonable; SHA256 recorded; no secrets/source in artifact.
6. Manual smoke on clean Windows: start exe, settings, add one account, start/stop, duplicate username rejected, PID reuse not killed, DPAPI isolation, no plaintext in logs.
7. CI green: `CI` (build windows-latest), `CodeQL` (csharp+actions), `Dependency Review`.
8. Update `CHANGELOG.md` and `ROADMAP.md`.

## Publish
```powershell
.\build-release.ps1
# output: src/CamfrogMultiID.App/bin/Release/net8.0-windows/win-x64/publish/
Get-FileHash src/CamfrogMultiID.App/bin/Release/net8.0-windows/win-x64/publish/CamfrogMultiID.exe -Algorithm SHA256
```

Artifact contains:
- `CamfrogMultiID.exe` (single-file)
- Runtime and native assets (self-contained)
- No `.pdb` policy: pdbs not shipped (deterministic build)

## Release creation
```powershell
git tag v1.0.0
git push origin v1.0.0
# GitHub release via workflow or manual: attach ZIP of publish folder + SHA256 + notes from CHANGELOG
```

## Rollback
- Re-tag previous `vX.Y.Z` or re-publish previous commit's artifact.
- No DB migrations to revert (SQLite additive); if schema changes, document downgrade path.
- Invalidate compromised artifacts, rotate DPAPI secrets if needed (re-enter passwords).

## Verification after publication
- Download artifact on clean machine, run, check version, check logs.
