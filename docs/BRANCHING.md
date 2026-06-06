# Branching policy

Process Bus Insight uses a single public development branch:

- `main` — stable public source, documentation, GitHub Pages content, CI, and release automation.

This keeps the repository simple for users who only want to clone, build, download, or inspect the current product state.

## Why one branch?

A public engineering tool should be easy to understand at first glance. A single `main` branch avoids confusion between `master`, feature-hardening branches, temporary release branches, and outdated work-in-progress branches.

## Recommended maintainer workflow

1. Develop changes locally.
2. Push directly to `main` only when the change is clean and buildable.
3. For risky work, use a private/local branch first, then squash or merge into `main` before publishing.
4. Use GitHub Releases for downloadable Windows packages instead of long-lived release branches.

## Automation expectation

The CI, GitHub Pages, and Windows portable release workflows are configured to target `main` only. Tag-based release runs still work for version tags such as `v1.2.3`.
