# Development

## Local setup

1. Clone the generated repository.
2. Copy `.env.example` to `.env` and populate local-only values.
3. Install the selected runtime and dependencies.
4. Replace placeholder `Makefile` targets with real project commands.
5. Run formatting, linting, tests, build, and security checks before opening a pull request.

## Quality expectations

- Keep changes small and reviewable.
- Add tests for behavior changes.
- Prefer deterministic and reproducible tooling.
- Do not commit secrets or local credentials.
- Do not weaken security or CI gates to obtain a passing build.

## Documentation

Update architecture, development, release, and ADR documentation when behavior or operational assumptions change.
