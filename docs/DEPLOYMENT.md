# Deployment — GitHub Pages and Release Automation

## GitHub Pages

The public landing page lives in `docs/index.html` and is designed for GitHub Pages.

Recommended Pages URL:

```text
https://masarray.github.io/DigSubAnalyzer/
```

The repository includes:

- `docs/index.html`
- `docs/styles.css`
- `docs/app.js`
- `docs/robots.txt`
- `docs/sitemap.xml`
- `docs/site.webmanifest`
- `docs/.nojekyll`

The workflow `.github/workflows/pages.yml` uploads the `docs` folder as a static Pages artifact.

## Release automation

The workflow `.github/workflows/release-package.yml` creates the Windows portable package and SHA256 sums. It can publish a GitHub Release when requested.

Recommended manual release flow:

1. Push the repository changes to GitHub.
2. Open **Actions**.
3. Run **Release Windows portable package**.
4. Use version `1.2.0-public-beta` or the next planned version.
5. Keep `publish_release=false` for a dry artifact build.
6. Set `publish_release=true` after verifying the artifact.

## About panel / topics

Use `scripts/set-github-about.ps1` to preview or apply the repository description, homepage, and topics using GitHub CLI.
