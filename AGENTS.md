# AGENTS.md — Camfrog Multi-ID Manager Repository Contract

## Mission

This repository is the source of truth for the Camfrog Multi-ID Manager Windows WPF application, its tests, build/release automation, security controls, and engineering documentation.

Agents operating in this repository must optimize for **correctness, security, reliability, compatibility, testability, and production readiness** while keeping changes minimal and reviewable.

---

## 0. Explicit Scope Gate — MUST RUN BEFORE PHASE 1

Before modifying any file, establish the task scope.

### In scope

Only changes directly justified by:

- observed bugs or reproducible failures
- incomplete behavior clearly implied by existing code/product behavior
- security or reliability defects
- missing regression coverage
- build/release/CI correctness
- dependency maintenance required for supported compatibility/security
- architecture changes required for correctness
- documentation/configuration drift
- obsolete or generic template artifacts
- production packaging/readiness requirements

### Out of scope unless explicitly required

Do not introduce unrelated:

- product features
- UI redesigns
- branding/marketing changes
- SaaS/cloud infrastructure
- unrelated integrations
- architecture rewrites
- dependency replacements without evidence
- speculative optimizations
- Docker/containerization unrelated to the product
- platform migration away from Windows/WPF

Do not turn a production-hardening task into a product redesign.

### Scope expansion

If an issue requires work outside the initial scope:

1. identify the evidence,
2. explain why it blocks correctness/security/release readiness,
3. choose the smallest safe change,
4. implement only what is required,
5. record the scope expansion in the final report.

Never silently expand scope.

---

## 0.1 Change-Safety Gate — MUST RUN BEFORE EACH MEANINGFUL CHANGE

For every non-trivial production change, assess:

```
Change:
Reason:
Affected Components:
Behavior Changed:
Data Impact:
Security Impact:
Compatibility Impact:
Test Impact:
Rollback Strategy:
Risk: LOW | MEDIUM | HIGH
```

### Low risk

Examples:

- documentation corrections
- analyzer-recommended syntax fixes
- test-only changes
- safe CI formatting/configuration corrections
- removal of proven obsolete artifacts

Normal validation is sufficient.

### Medium risk

Examples:

- production refactoring
- persistence changes
- process lifecycle changes
- dependency upgrades
- startup/shutdown changes
- configuration changes
- CI/release behavior changes

Require targeted tests, caller review, build validation, and regression coverage where practical.

### High risk

Examples:

- credential/security behavior
- process termination
- database schema/migrations
- authentication/authorization
- filesystem isolation
- release packaging
- destructive data operations
- framework/major dependency upgrades

Require root-cause analysis, impact analysis, regression/failure-path tests, full relevant validation, and rollback/recovery analysis.

---

## 0.2 Destructive-Change Gate

Never perform destructive operations without verifying their exact impact.

Protected operations include:

- deleting user data
- destructive database migrations
- deleting migrations/schema history
- killing external processes
- deleting source/configuration files
- rewriting Git history
- force-pushing
- removing security controls
- replacing configuration wholesale

Do not use destructive operations merely to obtain a clean build or working tree.

### Database safety

Never assume a development database represents production.

For migrations:

- preserve existing data where practical
- validate existing data against new constraints
- test upgrade and failure paths
- avoid irreversible changes unless explicitly required
- never silently drop user data

If safe migration semantics are unclear, stop that change and report the blocker instead of guessing.

### Process safety

Never terminate a process solely because a PID matches.

Validate, where available:

- PID
- executable identity
- process start time
- ownership/context
- expected profile/state

Fail closed when identity is ambiguous. Preserve PID-reuse protection.

---

## 0.3 Pre-Change Baseline

Before the first production-code modification, record:

- current branch
- current commit
- working-tree status
- solution/projects
- target frameworks
- runtime identifiers
- existing build result
- existing warnings/errors
- existing test result
- existing analyzer/security findings
- relevant CI failures

If pre-existing changes exist:

- inspect them
- preserve unrelated user work
- never reset/clean/overwrite them merely to simplify the task
- distinguish pre-existing changes from agent changes

---

## 0.4 Evidence-First Rule

Never claim that something is:

- fixed
- tested
- secure
- compatible
- regression-free
- production-ready

without actual evidence.

Use source inspection, build/test output, static analysis, security scans, CI results, or artifact inspection.

If a validation step was not executed, explicitly state:

```
NOT VERIFIED
```

Never infer a passing result.

---

## 0.5 Change Boundary Rule

For every changed file answer:

> Why does this file need to change for production readiness?

For every new file:

> What concrete requirement does it satisfy?

For every deleted file:

> Why is it obsolete, unsafe, duplicated, or incompatible?

Avoid unrelated churn.

---

## 0.6 Stop-and-Escalate Conditions

Stop only the affected change when:

- user data could be destroyed
- credential validity could be compromised
- migration semantics are ambiguous
- process identity cannot be safely established
- an external contract is unknown
- a breaking API change is required but compatibility is unknown
- security requirements conflict
- unrelated uncommitted work would be overwritten
- required production secrets/credentials are unavailable
- a high-risk change cannot be safely validated

Report:

```
BLOCKED CHANGE

Reason:
Risk:
Evidence:
What was attempted:
Required information/validation:
Safe work that can continue:
```

Continue unrelated safe work where possible.

---

## 1. Repository Operating Rules

- Read `README.md`, `CONTRIBUTING.md`, `SECURITY.md`, `ROADMAP.md`, and the closest `AGENTS.md` before editing.
- Keep code, configuration, filenames, commit messages, and technical documentation in English.
- Prefer the smallest complete and reviewable change.
- Never weaken CI, CodeQL, dependency review, analyzers, nullable checks, or release controls to make checks pass.
- Never commit credentials, tokens, private keys, production secrets, personal data, local databases, or Camfrog profile data.
- Treat executable paths, command arguments, profile paths, imported data, dependency metadata, and fork PR content as untrusted input.
- Preserve the Windows/.NET architecture unless a concrete requirement justifies an additional supported component.
- Reuse existing workflows/documents instead of creating overlapping alternatives.
- Use least-privilege GitHub Actions permissions.
- Do not claim production readiness until the final production gate passes.

---

## 2. Project-Specific Build Rules

- Production application targets `net8.0-windows` and `win-x64`.
- Normal development builds should not force a RuntimeIdentifier unless required.
- Production publishing uses `build-release.ps1` and must produce `CamfrogMultiID.exe`.
- MinGW/CMake/Wine/vcpkg are auxiliary tooling for native helper components/tests; they do not replace the supported .NET WPF production build.
- Process termination must remain fail-closed against PID reuse and executable identity mismatch.
- Passwords must remain protected using Windows DPAPI CurrentUser.
- Passwords must never be passed through command lines or logs.

---

## 3. Repository Audit Requirements

Before implementation, inspect:

- all source
- all projects
- solution configuration
- tests
- scripts
- PowerShell/Bash
- YAML/JSON/XML
- GitHub Actions
- CodeQL
- dependency configuration
- release tooling
- packaging
- documentation
- generated artifacts
- configuration
- resources

Search for:

```
TODO
FIXME
HACK
XXX
TBD
WIP
TEMP
placeholder
stub
mock
fake
NotImplementedException
NotSupportedException
return null
return default
throw new Exception
```

Also search for secrets and sensitive data patterns.

Classify findings before changing them. Fix genuine production defects.

---

## 4. Build/Analyzer Gate

Validate the complete solution graph.

Every project must have valid mappings for:

- `Debug|Any CPU`
- `Release|Any CPU`

Do not suppress solution configuration warnings such as MSB4121.

Run applicable:

```
dotnet restore
dotnet build .\CamfrogMultiID.sln -c Debug --no-restore
dotnet build .\CamfrogMultiID.sln -c Release --no-restore
dotnet test .\CamfrogMultiID.sln -c Release --no-build
```

Fix compiler/analyzer warnings at their root cause.

---

## 5. Architecture and Security

Maintain clear boundaries:

```
App
 ↓
Infrastructure
 ↓
Core
```

Audit:

- dependency direction
- persistence
- DPAPI credential protection
- process lifecycle
- PID reuse
- executable identity
- path validation
- command/argument injection
- filesystem permissions
- temporary files
- DLL search risks
- dependency vulnerabilities
- exception/log secret leakage
- async/concurrency behavior
- cancellation/timeouts
- resource disposal

Do not weaken security controls for convenience.

---

## 6. Testing Requirements

Test both happy and failure paths.

At minimum cover applicable:

- validation
- account lifecycle
- duplicate handling
- SQLite persistence
- concurrency
- credential protection
- corrupted credentials
- process identity
- stale/reused PID
- process disappearance
- access denied
- timeout
- cancellation
- invalid/missing paths
- startup/shutdown recovery
- UI command/ViewModel behavior

Every discovered bug should receive a regression test where practical.

---

## 7. CI/CD and Release

CI must validate the same build architecture used for production.

Verify:

- restore
- build
- test
- publish
- CodeQL
- dependency review
- least-privilege permissions
- action versions
- release tags
- artifact contents
- checksum
- no secrets/debug artifacts

The production artifact must be inspected, not merely generated.

---

## 8. Documentation Synchronization

Keep these synchronized with actual behavior:

```
README.md
ABOUT.md
AGENTS.md
CONTRIBUTING.md
SECURITY.md
CHANGELOG.md
ROADMAP.md
IMPLEMENTATION-CHECKLIST.md
docs/
.github/
```

Remove generic/template claims such as:

- Your Project
- Example Project
- Acme
- lorem ipsum
- replace this
- unfinished template language

Never document functionality that does not exist.

---

## 9. Change Workflow

For each logical change:

```
SCOPE GATE
↓
CHANGE-SAFETY GATE
↓
IMPLEMENT
↓
REGRESSION TEST
↓
BUILD
↓
STATIC ANALYSIS
↓
SECURITY VALIDATION
↓
DOCUMENTATION SYNC
↓
DIFF REVIEW
```

Prefer small atomic commits.

Use meaningful commit messages such as:

```
fix: correct solution release configuration mapping
fix: harden process identity validation
fix: prevent plaintext credential persistence
test: add process lifecycle regression coverage
ci: align release build with production publish
docs: synchronize production release documentation
```

---

## 10. Final Diff Safety Review

Before completion, inspect the complete diff.

Verify:

- no unrelated changes
- no accidental deletions
- no debug code
- no secrets
- no placeholder code
- no weakened security controls
- no unnecessary dependencies
- tests cover behavioral changes
- documentation matches implementation
- rollback implications are understood

The final diff must be minimal, intentional, reviewable, tested, and production-justified.

---

## 11. Final Production Gate

Do not declare production-ready unless all applicable checks pass:

- [ ] scope verified
- [ ] change-safety review complete
- [ ] no unsafe destructive changes
- [ ] solution configuration valid
- [ ] Debug build passes
- [ ] Release build passes
- [ ] zero unexpected warnings
- [ ] tests pass
- [ ] regression tests pass
- [ ] static analysis passes
- [ ] security checks pass
- [ ] CodeQL passes
- [ ] dependency review passes
- [ ] production publish succeeds
- [ ] executable verified
- [ ] artifact contents inspected
- [ ] no leaked secrets
- [ ] no TODO/unfinished production implementation
- [ ] CI matches production build
- [ ] release workflow validated
- [ ] documentation synchronized
- [ ] final diff reviewed
- [ ] rollback/recovery considerations reviewed

If any required item fails, continue fixing.

If validation is blocked, report it explicitly rather than claiming success.

---

## 12. Final Report

Use:

### Executive Summary

What actually changed.

### Bugs Found

For each:

```
Severity:
Location:
Root Cause:
Fix:
Regression Test:
```

### Security Findings

```
Finding:
Risk:
Fix:
Verification:
```

### Scope Summary

```
In Scope:
Out of Scope:
Scope Expansions:
```

### Change Safety Summary

```
Low-Risk Changes:
Medium-Risk Changes:
High-Risk Changes:
Destructive Changes:
Blocked Changes:
```

### Validation

```
Debug:
Release:
Tests:
Static Analysis:
Security:
CodeQL:
Dependency Review:
Publish:
Artifact Inspection:
```

### Documentation / CI

List changed files and workflows.

### Remaining Issues

List only genuine issues.

### Production Gate

Report every gate as PASS, FAIL, or NOT VERIFIED.

Never report a predicted success as an actual result.

---

## 13. Agent Execution Directive

You are an implementation agent.

Do not merely describe fixes.

Inspect, implement, test, validate, harden, synchronize, and review.

When discovering a related defect, investigate its root cause rather than patching only the visible symptom.

Do not ask for confirmation for routine, low-risk engineering fixes.

Use the smallest safe implementation that completely resolves the evidence-backed problem.

Start with:

```
PHASE 0 — SCOPE GATE
PHASE 0.1 — CHANGE-SAFETY GATE
PHASE 0.2 — PRE-CHANGE BASELINE
PHASE 1 — FULL REPOSITORY INVENTORY
PHASE 2 — BUG / TODO / SECURITY SCAN
PHASE 3 — ARCHITECTURE + BUILD GRAPH AUDIT
PHASE 4 — IMPLEMENTATION
PHASE 5 — TEST + STATIC ANALYSIS
PHASE 6 — SECURITY HARDENING
PHASE 7 — CI/CD + RELEASE VALIDATION
PHASE 8 — DOCUMENTATION SYNCHRONIZATION
PHASE 9 — FINAL DIFF + CHANGE-SAFETY REVIEW
PHASE 10 — FINAL PRODUCTION GATE
```
