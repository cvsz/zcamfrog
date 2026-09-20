# Roadmap

## Completed

- [x] .NET 8 WPF application baseline
- [x] SQLite persistence with WAL, busy timeout, foreign-key enforcement, and case-insensitive username uniqueness
- [x] DPAPI CurrentUser credential storage
- [x] Single-instance startup handling
- [x] PID/start-time/executable identity tracking
- [x] Fail-closed process termination
- [x] Runtime reconciliation
- [x] Add, start, stop, start-all, stop-all, and remove-account workflows
- [x] Self-contained win-x64 publishing
- [x] Regression tests
- [x] Windows CI build/test/publish gate
- [x] C# and GitHub Actions CodeQL analysis
- [x] Dependency review and Dependabot automation

## Next improvements

- [ ] Account editing with credential rotation and profile reset controls.
- [ ] Richer process diagnostics and per-account lifecycle history.
- [ ] Signed release artifacts and provenance attestations.
- [ ] UI smoke tests on a Windows runner where interactive WPF execution is supported.
- [ ] Validate profile isolation against each supported Camfrog client version.
- [ ] Add automated validation for the Ubuntu MinGW/Wine/vcpkg helper toolchain.
- [ ] Add release artifact checksums and documented verification instructions.
