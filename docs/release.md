# Release

## Versioning

Use an explicit versioning policy. Semantic Versioning is recommended for reusable software unless the project has a better-defined scheme.

## Release checklist

1. Ensure required CI and security checks pass.
2. Update `CHANGELOG.md`.
3. Confirm migrations and compatibility notes.
4. Verify deployment and rollback procedures.
5. Create and push the release tag according to project policy.
6. Publish artifacts only from trusted workflows.
7. Verify the release after publication.

## Rollback

Document how to restore the last known-good version, revert migrations safely, invalidate compromised artifacts, and communicate operational impact.
