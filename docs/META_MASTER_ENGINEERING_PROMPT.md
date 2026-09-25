I reviewed the current `main` of `cvsz/zcamfrog` at commit `8929bfb1b9af194fb58ab5f30595fd995d33d858` and cross-checked the repository structure, README/AGENTS/ROADMAP/CHANGELOG, CI/release workflows, project files, process/storage design, and recent commits. The repository is substantially more mature than a basic prototype, but I would **not yet label it production-complete**.

[cvsz/zcamfrog](https://github.com/cvsz/zcamfrog?utm_source=chatgpt.com)

## 1. Executive assessment

| Area                           | Assessment                                     |
| ------------------------------ | ---------------------------------------------- |
| Architecture                   | **Good**                                       |
| Windows/.NET build model       | **Good**                                       |
| Process safety                 | **Strong, but needs adversarial testing**      |
| DPAPI secret boundary          | **Good**                                       |
| SQLite persistence             | **Good, migration needs hardening**            |
| Sandboxie integration          | **Experimental / highest-risk area**           |
| UI lifecycle                   | **Improved substantially**                     |
| CI                             | **Good, but has suspicious fallback behavior** |
| Release pipeline               | **Good, not fully supply-chain hardened**      |
| Documentation                  | **Inconsistent**                               |
| Roadmap truthfulness           | **Inconsistent**                               |
| Test evidence                  | **Insufficiently reconciled**                  |
| Cross-platform/MinGW additions | **Scope drift / questionable**                 |
| Production readiness           | **NO-GO pending verification**                 |

The repo itself explicitly establishes fail-closed process and credential invariants in `AGENTS.md`, which is the right architectural direction.

---

# 2. Bugs / inconsistencies I found

### P0 — Production evidence is internally inconsistent

The documentation disagrees about test counts.

`README.md` currently claims **94 tests pass**.

But `CHANGELOG.md` says:

* 86 xUnit tests
* then later says README synchronized to **85**
* current unreleased changes continue adding tests/features.

This is a concrete documentation integrity failure.

**Required fix:**

```text
actual discovered tests
actual passed tests
actual skipped tests
actual failed tests
exact commit SHA
exact CI run
```

must become one source of truth.

Do **not** let OpenCode manually guess the number.

---

### P0 — Roadmap contradicts itself

`ROADMAP.md` says:

* localization is `[x]`
* accessibility is `[x]`
* provenance is `[x]`
* installer is `[~]`

but the **Future considerations** section still lists:

* Localization
* Accessibility
* Signed artifacts and provenance

as future work.

This makes the roadmap unreliable as an execution authority.

**Fix:** perform a roadmap reconciliation against actual code + CI evidence.

---

### P0 — Process identity requires adversarial testing, not just unit tests

The architecture claims:

> PID + start-time + executable-path validation

and fail-closed process termination.

That's good.

But the critical security invariant is:

```text
PID reused
+
different process
+
same/similar executable
=
MUST NEVER KILL
```

The tests need to prove this under realistic race conditions.

I would specifically require tests for:

1. PID reuse.
2. process exits between validation and kill.
3. process restarts between validation and kill.
4. executable path inaccessible.
5. start-time unavailable.
6. `MainModule` inaccessible.
7. symlink/reparse-point executable path.
8. Sandboxie wrapper PID vs actual child PID.
9. stale database state after application crash.
10. Stop while Start is concurrently executing.

The existing tests themselves acknowledge cases where `MainModule` may be inaccessible.

That is precisely where fail-closed behavior needs to be strongest.

---

# 3. Sandboxie is the biggest technical risk

Recent commits show that Sandboxie behavior has repeatedly required corrections:

* box creation mechanism changed to `SbieIni`
* box naming required fixes
* stopping `Start.exe` alone was insufficient
* `/terminate` had to be added
* service/driver readiness had to be introduced.

That history is important.

This is not evidence that the integration is bad; it is evidence that **Sandboxie is the least stable integration boundary in the application**.

The current README explicitly describes Sandboxie multi-instance support as experimental.

Therefore OpenCode must treat:

```text
Camfrog client
        ↓
Sandboxie Start.exe
        ↓
Sandboxie box
        ↓
Camfrog process
```

as a state machine, not simply:

```text
Process.Start()
```

Required states:

```text
NotConfigured
Configured
ServiceUnavailable
DriverUnavailable
BoxMissing
BoxCreating
BoxReady
Starting
Running
Stopping
TerminatingBox
Stopped
Failed
Unknown
```

And every transition must be observable.

---

# 4. Potential security weakness: ACL failure is intentionally ignored

`AppPaths.RestrictSecretsAccess()` deliberately catches ACL failures and allows startup to continue.

That may be acceptable because DPAPI `CurrentUser` is the primary secret boundary.

But the documentation currently describes the secrets directory as being restricted to the current user.

Those are not equivalent guarantees.

You need to distinguish:

```text
DPAPI protection = cryptographic boundary
ACL = filesystem access-hardening layer
```

If ACL application fails, the application should report:

```text
DPAPI protected
filesystem ACL hardening unavailable
```

rather than silently presenting the same security state.

---

# 5. Database migration is too primitive

The migration strategy is effectively:

```text
CREATE TABLE IF NOT EXISTS
+
PRAGMA table_info
+
ALTER TABLE ADD COLUMN
```

This is acceptable for small local applications, but it isn't a real migration framework.

Risks:

* partially completed migrations
* future column dependencies
* indexes/constraints not migrated
* schema version ambiguity
* migration order ambiguity
* interrupted migration
* backup made during migration
* old database with malformed schema
* incompatible future releases

The repository calls this "forward-compatible migration," but the implementation is currently closer to **incremental schema repair**.

OpenCode should introduce:

```text
PRAGMA user_version
```

or an explicit migration table:

```text
schema_migrations
------------------
version
applied_utc
checksum
```

and transactional migrations.

---

# 6. `UpdateDetails()` needs stronger domain validation

The database layer trims values but relies heavily on UI validation.

For example, the DB method accepts:

```text
displayName
username
enabled
```

and performs the update.

The unique index protects username uniqueness, but the persistence boundary should also enforce domain constraints.

The principle should be:

> UI validation improves UX; domain/persistence validation guarantees correctness.

OpenCode should test:

* empty username
* whitespace username
* excessively long username
* invalid Unicode/control characters
* normalization collisions
* leading/trailing whitespace
* invalid display name
* duplicate usernames with Unicode edge cases

---

# 7. Room URL validation needs one canonical implementation

The project correctly restricts room URLs to the `camfrog:` scheme. `NormalizeRoomUrl()` is used by account editing, and tests exist for it.

But the database `SetRoomUrl()` itself accepts an arbitrary string. The current architecture therefore has:

```text
UI → validation → DB
```

rather than:

```text
Domain boundary → validation → DB
```

That matters if another code path eventually calls `SetRoomUrl()`.

Canonical rule:

```text
camfrog://...
```

must be enforced at the domain/service boundary.

---

# 8. CI contains a dangerous fallback

This is particularly important.

The CI workflow does:

```text
dotnet build solution
```

and if Release fails:

```text
build App project directly
```

The stated purpose appears to be recovering from solution configuration problems. But this can mask a broken solution.

That conflicts with the project's own philosophy of verifying solution integrity.

The correct CI behavior is:

```text
solution build failure
        ↓
FAIL
```

not:

```text
solution build failure
        ↓
maybe App build passes
        ↓
SUCCESS
```

Otherwise `CamfrogMultiID.Infrastructure`, tests, or another project can potentially be excluded while CI still succeeds.

This should be removed.

---

# 9. CI doesn't prove enough about the actual release artifact

The CI workflow builds and publishes the executable and calculates a SHA-256, but the artifact verification should become explicit.

Current CI does verify that:

```text
CamfrogMultiID.exe
```

exists.

But production validation should additionally verify:

* expected file count
* executable PE validity
* architecture = x64
* version metadata
* assembly version
* single-file publish
* no debug symbols unexpectedly included
* no plaintext secrets
* deterministic metadata where intended
* checksum
* SBOM
* provenance
* artifact contents against allowlist

---

# 10. GitHub Actions aren't actually pinned to immutable SHAs

`AGENTS.md` says:

> Pin GitHub Actions to least privilege; prefer maintained first-party/verified actions.

But the workflows use:

```yaml
actions/checkout@v7
actions/setup-dotnet@v6
actions/upload-artifact@v7
actions/attest-build-provenance@v4
```

Those are **major-version tags**, not immutable SHA pins.

That's not supply-chain pinning.

For a stronger production posture:

```yaml
uses: actions/checkout@<full-40-char-sha> # v7.x
```

and Dependabot can maintain the SHA references.

---

# 11. Release permissions are broader than necessary

The release workflow grants:

```yaml
contents: write
id-token: write
attestations: write
```

The latter two are expected for provenance.

But release automation should explicitly document why each permission exists and ensure no job receives permissions it doesn't need.

Better:

```text
job-level permissions
+
minimal step scopes
+
immutable action SHAs
```

---

# 12. MinGW/Wine/CMake appears to be scope drift

The latest merge added:

```text
scripts/bootstrap-ubuntu-mingw-wine-vcpkg.sh
toolchain-x86_64-w64-mingw32.cmake
```

The commit itself says this is auxiliary and that the supported production build remains Windows/.NET.

But GitHub code search finds **no `CMakeLists.txt`**.

That creates a suspicious state:

```text
cross-compilation bootstrap
        +
toolchain
        +
no actual CMake project
```

This is incomplete unless those tools are genuinely required for another component.

Given the repository's own `AGENTS.md` says there is no Linux/macOS support and no need to add it without explicit scope, this should either:

### Option A — Remove it

Keep repository strictly:

```text
Windows
.NET 8
WPF
```

### Option B — Properly isolate it

Move it under something like:

```text
tools/native/
```

and document exactly:

```text
why it exists
what it builds
what artifact it produces
how CI validates it
why MinGW is required
why Wine is required
```

Right now it looks like infrastructure without a demonstrated consumer.

---

# 13. Dockerfile is technically misleading

The Dockerfile intentionally fails:

```dockerfile
RUN echo "CamfrogMultiID is Windows-only WPF..." && exit 1
```

The README/AGENTS say Docker isn't supported.

That's understandable, but a Dockerfile whose purpose is to **always fail** can confuse automated scanners and developers.

Better:

```text
remove Dockerfile
```

or replace it with explicit documentation:

```text
docs/build-support.md
```

If the file is retained, CI should explicitly verify it is an intentional marker rather than treating it as a build target.

---

# 14. Documentation chronology is messy

The README contains:

```text
V20
V17
V16
V15
V14
...
```

rather than a coherent current-state document.

This is a classic symptom of iterative patching.

It makes it difficult to answer:

> What does version 1.3.0 actually contain?

The README should be rewritten around **current architecture**, not historical patch notes.

Historical changes belong in:

```text
CHANGELOG.md
```

---

# 15. Roadmap needs a real execution model

The current roadmap is too flat:

```text
[x]
[x]
[~]
[ ]
```

For a production application, I'd use:

```text
P0 security/correctness
P1 release blockers
P2 reliability
P3 UX
P4 future
```

with evidence:

```markdown
- [x] Sandboxie stop lifecycle
  Evidence: commit <sha>, tests <N>, CI run <url>
```

This prevents "checkbox theater."

---

# 16. Production readiness decision

### Current status

**NO-GO**

Not because the architecture is poor.

Quite the opposite: the foundation is good.

The reason is that the repo has too many **evidence/reconciliation gaps**:

```text
documentation ≠ implementation state
test count ≠ consistent
roadmap ≠ current state
Sandboxie = high-risk integration
CI fallback can mask solution failures
cross-platform tooling = unclear scope
release security = not immutable enough
migration system = immature
```

---

# 17. What I would have OpenCode do

Do **not** tell OpenCode:

> "fix everything."

That encourages uncontrolled refactoring.

Instead, give it a strict autonomous engineering contract.

Below is the **Meta Master Prompt** I recommend.

# ZCAMFROG — META MASTER ENGINEERING PROMPT

## Autonomous Deep Review → Bug Hunt → Hardening → Validation → Production Readiness

You are the principal software engineer, security engineer, reliability engineer, QA engineer, release engineer, and technical reviewer for:

**Repository:** `cvsz/zcamfrog`
**Application:** Camfrog Multi-ID Manager
**Platform:** Windows x64
**Framework:** .NET 8 / WPF
**Repository default branch:** `main`

Your objective is to bring the repository to a genuinely production-ready state through evidence-driven engineering.

Do NOT optimize for number of commits, number of files changed, or apparent progress.

Optimize for:

* correctness
* security
* reliability
* deterministic behavior
* testability
* maintainability
* release integrity
* truthful documentation
* smallest safe change
* verifiable production evidence

---

# 0. ABSOLUTE OPERATING PRINCIPLES

## 0.1 Evidence before implementation

Never assume that README, CHANGELOG, ROADMAP, comments, issue descriptions, or previous agent claims are correct.

Treat repository code and reproducible command output as authoritative.

Every important claim must be classified:

* VERIFIED
* INFERRED
* UNKNOWN
* BLOCKED

Never convert UNKNOWN into VERIFIED.

Never invent:

* production evidence
* successful tests
* security guarantees
* release validation
* user acceptance
* Camfrog client behavior
* Sandboxie behavior
* external API behavior

---

# 1. INITIAL REPOSITORY RECONNAISSANCE

Before modifying anything:

1. Confirm repository root.
2. Confirm current branch.
3. Confirm exact HEAD SHA.
4. Confirm working tree state.
5. Confirm remote configuration.
6. Read:

```text
AGENTS.md
README.md
ROADMAP.md
CHANGELOG.md
SECURITY.md
GOVERNANCE.md
CONTRIBUTING.md
IMPLEMENTATION-CHECKLIST.md
docs/architecture.md
docs/development.md
docs/release.md
docs/troubleshooting.md
docs/sandboxie.md
```

7. Inventory:

```text
src/
tests/
.github/
scripts/
docs/
*.sln
*.csproj
*.ps1
*.cmd
*.yml
*.yaml
*.json
```

8. Identify generated files.
9. Identify build artifacts.
10. Identify secrets/configuration files.
11. Identify all external dependencies.
12. Identify all process-launching code.
13. Identify all filesystem writes.
14. Identify all database migrations.
15. Identify all credential/DPAPI operations.
16. Identify all Sandboxie operations.
17. Identify all network operations.
18. Identify all release operations.

Do not edit code during reconnaissance.

---

# 2. ESTABLISH CURRENT TRUTH

Create an internal evidence matrix:

| Area         | Repository claim | Actual evidence | Status           |
| ------------ | ---------------- | --------------- | ---------------- |
| Build        | ...              | ...             | VERIFIED/UNKNOWN |
| Tests        | ...              | ...             | ...              |
| Localization | ...              | ...             | ...              |
| Sandboxie    | ...              | ...             | ...              |
| Backup       | ...              | ...             | ...              |
| Release      | ...              | ...             | ...              |
| Security     | ...              | ...             | ...              |

Resolve contradictions before declaring production readiness.

Especially reconcile:

* README test count
* CHANGELOG test count
* actual test discovery count
* roadmap completion
* current version
* executable version metadata
* release tag
* CI evidence

---

# 3. NO-GO CONDITIONS

Never declare production-ready if any of these remain unresolved:

* compile errors
* warnings treated as errors
* failing tests
* skipped tests without justification
* broken solution project mapping
* broken migration
* plaintext secret leakage
* process identity ambiguity
* unsafe process termination
* Sandboxie lifecycle ambiguity
* backup restore corruption
* path traversal vulnerability
* command injection
* argument quoting vulnerability
* uncontrolled child process
* UI startup crash
* unreconciled roadmap
* false documentation claims
* release artifact not verified
* release artifact not checksummed
* security scan failures
* dependency review failures
* unresolved critical/high security findings
* production code containing TODO/FIXME/stub behavior
* CI success dependent on fallback paths that can hide real failures

---

# 4. BUG HUNT

Perform a deep bug hunt across:

## 4.1 Process lifecycle

Review every path involving:

```text
Process.Start
Process.GetProcessById
Kill
CloseMainWindow
WaitForExit
MainModule
StartTime
ProcessName
ExecutablePath
PID
Sandboxie
Start.exe
SbieIni
service
driver
auto-restart
reconciliation
shutdown
startup
```

Test:

* PID reuse
* process exit races
* process restart races
* stale DB records
* executable replacement
* executable path mismatch
* inaccessible MainModule
* inaccessible StartTime
* permission failures
* process tree termination
* concurrent Start
* concurrent Stop
* Start while Stop is running
* Stop while Start is running
* application crash while child is running
* manager restart while client is running
* Sandboxie wrapper process disappearance
* sandbox contents surviving wrapper termination

RULE:

> If identity cannot be proven, fail closed.

Never kill a process on PID alone.

---

# 5. SANDBOXIE STATE MACHINE

Treat Sandboxie as an external unreliable dependency.

Implement/test explicit states:

```text
Disabled
Configured
Unavailable
ServiceUnavailable
DriverUnavailable
BoxMissing
BoxCreating
BoxReady
Starting
Running
Stopping
TerminatingBox
Stopped
Failed
Unknown
```

Verify:

* Start.exe existence
* SbieIni existence
* service state
* driver/readiness state
* box existence
* box creation
* duplicate box creation
* invalid box names
* Unicode usernames
* underscores
* spaces
* special characters
* maximum name length
* box deletion
* box termination
* client termination
* stale box state
* manager crash
* Sandboxie restart
* service unavailable
* elevation failure
* UAC cancellation

Never claim Sandboxie support is production-grade unless the lifecycle is tested.

---

# 6. COMMAND-LINE / ARGUMENT SECURITY

Audit all command construction.

Inputs include:

```text
username
profile path
executable path
room URL
Sandboxie box name
argument template
settings
filesystem paths
```

Rules:

* Never concatenate untrusted arguments unsafely.
* Use one canonical Windows command-line quoting implementation.
* Never place passwords in command lines.
* Never place passwords in logs.
* Never place secrets in exceptions.
* Never place secrets in crash dumps intentionally.
* Never permit arbitrary executable injection through templates.
* Validate allowed placeholders.
* Reject unsupported placeholders unless explicitly approved.
* Test quotes, spaces, Unicode, backslashes, trailing slashes, and empty values.

---

# 7. ROOM URL SECURITY

Canonicalize room URL validation in one domain/service boundary.

Allowed scheme:

```text
camfrog:
```

Reject:

```text
http:
https:
file:
shell:
javascript:
powershell:
cmd:
custom unknown schemes
```

Test:

* whitespace
* mixed case
* malformed URI
* encoded values
* control characters
* CR/LF
* quotes
* backslashes
* command injection strings
* extremely long input
* unexpected authority/path/query structures

Database methods must not rely solely on UI validation.

---

# 8. DATABASE HARDENING

Audit:

```text
DatabaseService
schema initialization
migrations
indexes
constraints
transactions
locking
backup
restore
concurrency
corrupt database handling
```

Prefer explicit schema versioning using:

```text
PRAGMA user_version
```

or a migration table.

Migrations must be:

* ordered
* idempotent
* transactional where possible
* testable
* failure-safe
* backwards-aware
* documented

Test:

* fresh database
* every known historical schema
* migration success
* migration interruption
* malformed database
* duplicate username
* invalid rows
* locked DB
* concurrent access
* restore over live DB

---

# 9. SECRET MANAGEMENT

Verify:

```text
DPAPI CurrentUser
```

for stored passwords.

Never:

```text
plaintext password in SQLite
plaintext password in settings.json
password in command line
password in logs
password in exception
password in diagnostics bundle
password in backup metadata
```

Verify:

* save
* read
* overwrite
* delete
* missing secret
* corrupt secret
* wrong Windows user
* ACL failure
* backup
* restore
* account deletion
* password rotation

Distinguish:

```text
DPAPI cryptographic protection
```

from:

```text
filesystem ACL hardening
```

Do not report ACL hardening as successful if ACL configuration failed.

---

# 10. BACKUP / RESTORE

Audit:

```text
VACUUM INTO
zip generation
zip validation
restore
ClearAllPools
path traversal
absolute paths
UNC paths
symlinks/reparse points
malicious archive entries
partial restore
interrupted restore
disk-full behavior
```

Require:

* archive allowlist
* canonicalized destination
* traversal prevention
* no arbitrary extraction
* no executable overwrite
* no secret leakage
* atomic replacement where practical
* rollback or recoverable failure

Test malicious ZIP entries such as:

```text
..\evil
../evil
C:\evil
\\server\share\evil
/absolute/path
nested traversal
duplicate entries
oversized entries
unexpected file types
```

---

# 11. UI RELIABILITY

Audit every WPF event handler.

Pay special attention to:

```text
InitializeComponent
Loaded
SelectionChanged
TextChanged
Timer
DispatcherTimer
Closing
Closed
F5
Delete
Enter
Start All
Stop All
Auto-start
Refresh
Settings
Backup
Restore
```

Ensure event handlers cannot execute against partially initialized state.

No UI thread crashes.

No unbounded synchronous blocking.

No database calls that freeze the UI unnecessarily.

No timer re-entrancy.

No concurrent refresh races.

---

# 12. AUTO-RESTART

Verify the restart policy is mathematically correct.

Expected behavior:

```text
manual start/stop
    → reset restart budget

unexpected process exit
    → increment restart budget

>3 restarts within 10 minutes
    → pause
    → Error
    → no infinite loop
```

Test:

* crash loop
* rapid exit
* manual stop
* manual start
* application restart
* clock changes
* timer overlap
* process exits during restart
* Sandboxie failure during restart

---

# 13. LOCALIZATION

Verify:

```text
English
Thai
```

for:

* all windows
* dialogs
* context menus
* status labels
* validation errors
* process errors
* Sandboxie errors
* backup errors
* startup errors
* accessibility names

Require automated key parity:

```text
Strings.resx == Strings.th.resx
```

No missing values.

No empty translations.

No mojibake.

No invalid control characters.

---

# 14. ACCESSIBILITY

Verify:

* keyboard-only navigation
* focus order
* screen-reader names
* buttons
* grids
* status indicators
* error states
* dialogs
* language changes

Do not claim "accessible" based only on `AutomationProperties.Name`.

Perform a practical keyboard/UI audit.

---

# 15. CI HARDENING

CI must fail when the solution fails.

Do NOT use:

```powershell
if solution build fails:
    build App directly
    continue
```

unless there is an explicit separate diagnostic job.

Production CI should be:

```text
restore
→ validate solution
→ build Debug
→ build Release
→ test
→ publish
→ artifact validation
→ security validation
```

No silent fallback.

---

# 16. GITHUB ACTIONS SUPPLY-CHAIN SECURITY

Audit all:

```yaml
uses:
```

Prefer immutable SHA references.

Example:

```yaml
uses: actions/checkout@<40-character-sha> # vX.Y.Z
```

Use Dependabot to update pinned SHAs.

Require least-privilege permissions.

Separate build and release permissions.

Never expose secrets to pull requests from forks.

---

# 17. RELEASE VALIDATION

Before release:

```text
restore
build
test
publish
artifact inspection
checksum
SBOM
provenance
security scan
dependency review
```

Verify executable:

```text
PE = valid
Architecture = x64
Version = expected
Product = expected
Company = expected
```

Verify artifact contains only expected files.

Verify checksum.

Verify provenance.

Never overwrite an immutable release artifact without explicit release policy.

---

# 18. CROSS-PLATFORM / MINIGW / WINE REVIEW

Determine whether:

```text
scripts/bootstrap-ubuntu-mingw-wine-vcpkg.sh
toolchain-x86_64-w64-mingw32.cmake
```

have a real production consumer.

If there is no CMake project or native component:

* mark as scope drift
* recommend removal or isolation
* do not pretend it is part of the production build

Do not expand project scope merely because tooling exists.

---

# 19. DO NOT ADD FEATURES DURING BUG FIXING

Unless required to fix a demonstrated defect, do not introduce:

* network services
* cloud sync
* telemetry
* new authentication
* web server
* API
* Linux support
* macOS support
* custom sandbox driver
* Camfrog protocol bypass
* CAPTCHA bypass
* credential injection
* licensing bypass

Maintain the project's stated security boundary.

---

# 20. STATIC ANALYSIS

Run and inspect:

```powershell
dotnet format --verify-no-changes
dotnet build -c Debug
dotnet build -c Release
dotnet test
dotnet test --collect:"XPlat Code Coverage"
dotnet publish ...
```

Also inspect:

```text
TODO
FIXME
HACK
XXX
NotImplementedException
throw new NotImplementedException
return null
return false
placeholder
stub
mock production path
```

Do not blindly delete TODOs.

Classify each:

```text
intentional
non-production
known limitation
real incomplete implementation
```

Fix real production incompleteness.

---

# 21. TEST STRATEGY

Every discovered bug should receive a regression test whenever practical.

Tests must cover:

### Unit

* domain validation
* quoting
* URL validation
* migration
* credential lifecycle

### Integration

* SQLite
* filesystem
* DPAPI
* backup/restore
* settings

### Process

* fake executable
* process lifecycle
* PID identity
* stale process
* concurrent start/stop

### Sandboxie

* mocked external commands where possible
* real Windows integration tests where available
* service unavailable
* box missing
* box creation
* box termination

### UI

* startup
* initialization
* refresh
* localization
* accessibility metadata

---

# 22. TEST COUNT MUST NEVER BE HAND-MAINTAINED

Do not manually write:

```text
94 tests
```

or:

```text
86 tests
```

in README.

Generate the actual count from the test runner or CI.

Documentation should state:

```text
Tests: see latest CI evidence
```

or generate the number automatically during release documentation generation.

---

# 23. DOCUMENTATION RECONCILIATION

After implementation:

Rewrite stale documentation.

README must describe:

```text
current product
current architecture
current requirements
current installation
current build
current publish
current limitations
current security boundary
current Sandboxie status
current release status
```

CHANGELOG must contain historical changes.

ROADMAP must contain only actual future work.

Do not duplicate historical implementation notes into README.

---

# 24. DEFINITION OF DONE

A change is complete only when:

```text
[ ] implementation complete
[ ] no known correctness defect
[ ] regression test exists
[ ] unit tests pass
[ ] integration tests pass
[ ] Debug build passes
[ ] Release build passes
[ ] publish passes
[ ] artifact exists
[ ] artifact validated
[ ] security checks pass
[ ] dependency review passes
[ ] CodeQL passes
[ ] documentation reconciled
[ ] roadmap reconciled
[ ] changelog updated
[ ] no secret leakage
[ ] no production TODO/stub
[ ] exact commit SHA recorded
```

---

# 25. WORKFLOW

Execute work in vertical slices.

For each slice:

1. Inspect.
2. Reproduce.
3. Identify root cause.
4. Write regression test.
5. Implement smallest safe fix.
6. Run focused tests.
7. Run full tests.
8. Run build.
9. Run publish if relevant.
10. Review diff.
11. Security review.
12. Documentation update.
13. Commit.
14. Revalidate exact HEAD.
15. Continue to next highest-priority defect.

Do not create giant speculative refactors.

---

# 26. PRIORITY ORDER

Use this order:

## P0

Security / process termination / credential leakage / corruption

## P1

Build failures / CI false positives / release failures / data loss

## P2

Sandboxie correctness / restart lifecycle / backup restore

## P3

Database migrations / UI reliability / localization / accessibility

## P4

Documentation / developer ergonomics / cleanup

## P5

Future features

Never work on P5 while P0/P1 remains unresolved.

---

# 27. FINAL PRODUCTION GATE

At the end produce:

```text
ZCAMFROG PRODUCTION READINESS REPORT

HEAD:
VERSION:

BUILD:
Debug:
Release:
Publish:

TESTS:
Discovered:
Passed:
Failed:
Skipped:

SECURITY:
Credential boundary:
Process identity:
Command injection:
Path traversal:
Secrets scan:
CodeQL:
Dependency review:

SANDBOXIE:
Service:
Driver:
Box lifecycle:
Start:
Stop:
Recovery:

DATA:
Schema version:
Migration:
Backup:
Restore:

RELEASE:
Artifact:
SHA256:
Architecture:
Version metadata:
SBOM:
Provenance:

DOCUMENTATION:
README:
ROADMAP:
CHANGELOG:
SECURITY:

OPEN FINDINGS:
P0:
P1:
P2:
P3:

PRODUCTION DECISION:
GO / NO-GO

REASON:
...

VERIFIED EVIDENCE:
...

UNKNOWN / UNVERIFIED:
...
```

Never output `GO` if a P0/P1 issue remains.

Never output `GO` merely because CI is green.

---

# 28. FINAL RULE

The goal is NOT:

> make the repository look production-ready.

The goal is:

> make the repository demonstrably production-ready.

If evidence contradicts documentation, fix the documentation.

If evidence contradicts an assumption, fix the assumption.

If the implementation is wrong, fix the implementation.

If a requirement cannot be verified, mark it UNKNOWN.

If a production claim cannot be proven, do not make the claim.

**Correctness > completeness > convenience > speed.**

## My recommended OpenCode execution order

I would run the prompt in **four passes**, rather than allowing one giant autonomous mutation:

### Pass 1 — Audit only

```text
NO CODE CHANGES
produce findings + evidence matrix
```

### Pass 2 — P0/P1

```text
security
process lifecycle
CI false-success paths
data integrity
release correctness
```

### Pass 3 — P2/P3

```text
Sandboxie
restart
backup/restore
migration
UI
localization
accessibility
```

### Pass 4 — Release gate

```text
full clean build
full test
publish
artifact inspection
CodeQL
dependency review
documentation reconciliation
final GO/NO-GO
```

### Most important immediate fixes

If I were directing the next engineering cycle, I'd prioritize:

1. **Remove the CI Release-build fallback.**
2. **Reconcile the 85/86/94 test-count contradiction.**
3. **Rewrite ROADMAP/README into a single truthful current state.**
4. **Deep adversarial testing of PID/start-time/executable identity.**
5. **Treat Sandboxie as an explicit state machine and test failure/recovery paths.**
6. **Add real database schema versioning/migrations.**
7. **Make room URL/domain validation independent of UI.**
8. **Audit every command-line construction path.**
9. **Pin GitHub Actions to immutable SHAs.**
10. **Decide whether the MinGW/Wine/CMake additions are real scope or remove them.**
11. **Add release artifact/SBOM/PE/architecture verification.**
12. **Only after those pass, issue a production GO decision.**

The strongest positive finding is that the repository already has the right **security philosophy**—particularly fail-closed process identity, DPAPI `CurrentUser`, explicit Camfrog non-goals, and regression testing.  The main problem now is moving from **"many hardening changes have been implemented"** to **"the entire system has been independently and consistently proven."**
