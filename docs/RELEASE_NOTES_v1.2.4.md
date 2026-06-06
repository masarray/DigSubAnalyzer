# Release Notes — v1.2.4 Pages deployment fix

This maintenance release hardens the GitHub Pages deployment path for the public landing page.

## Changed

- Updated `.github/workflows/pages.yml` to run on every push to `main` and manual dispatch.
- Added a pre-deploy verification step for required landing page files.
- Added `scripts/configure-github-pages-actions.ps1` to switch the repository Pages source to GitHub Actions.
- Added `scripts/diagnose-github-pages.ps1` to inspect branch, Pages configuration, workflow status, and public URL status.
- Updated `docs/DEPLOYMENT.md` with the exact Pages and branch settings required for a clean public deployment.

## Expected result

The landing page should be deployed at:

```text
https://masarray.github.io/DigSubAnalyzer/
```

The repository should use:

```text
Default branch: main
Pages source: GitHub Actions
```
