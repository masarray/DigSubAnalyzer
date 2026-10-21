# Deployment

Process Bus Insight uses a static GitHub Pages landing page stored in `docs/` and deployed by `.github/workflows/pages.yml`.

## Required GitHub Pages setting

Use **GitHub Actions** as the Pages source:

```text
Repository -> Settings -> Pages -> Build and deployment -> Source -> GitHub Actions
```

Do not use `Deploy from a branch` for this repository. Branch publishing can leave the site tied to an old branch such as `master`, which can produce a successful-looking Pages build but still serve a 404 at the public URL after the repository has moved to `main`.

## Required branch setting

The public branch should be:

```text
main
```

Set it in GitHub:

```text
Repository -> Settings -> Branches -> Default branch -> main
```

Or with GitHub CLI:

```powershell
gh repo edit masarray/DigSubAnalyzer --default-branch main --delete-branch-on-merge=true
```

## Latest stable Windows installer

The landing page must lead with a **direct installer .exe** link, never an Actions artifact or the portable ZIP. At deployment time, all three primary download links (header, hero, and Download card) have a stable `v1.4.2` asset fallback. `docs/app.js` queries GitHub `/releases/latest` client-side and upgrades the buttons to the latest non-draft, non-prerelease version **only if** its matching `ProcessBusInsight-vX.Y.Z-win-x64-setup.exe` asset exists. Missing API access retains the already-published fallback.

The portable ZIP, SHA256 checksum, and release notes remain secondary links. `scripts/verify-landing-release.py` checks that the pinned fallback matches `Directory.Build.props`, that structured metadata/README are synchronized and that no primary CTA silently sends engineers to ZIP or the Releases list. The Pages workflow also runs `node --check docs/app.js` before deployment. When publishing a new stable version, update the pinned fallback URLs and version in the repository even though runtime discovery can follow the latest release automatically.

Latest public stable release: https://github.com/masarray/DigSubAnalyzer/releases/latest

## Deploy manually

After pushing the latest repository content to `main`, run:

```powershell
gh workflow run pages.yml --repo masarray/DigSubAnalyzer --ref main
```

Then check:

```powershell
gh run list --repo masarray/DigSubAnalyzer --workflow pages.yml --limit 5
```

Public URL:

```text
https://masarray.github.io/DigSubAnalyzer/
```

## Configure Pages with script

Preview:

```powershell
.\scripts\configure-github-pages-actions.ps1
```

Apply:

```powershell
.\scripts\configure-github-pages-actions.ps1 -Apply
```

Run diagnosis:

```powershell
.\scripts\diagnose-github-pages.ps1
```

## How the workflow works

The workflow:

1. runs on push to `main` and manual dispatch,
2. verifies required files under `docs/`,
3. uploads `docs/` as the GitHub Pages artifact,
4. deploys the artifact to the `github-pages` environment.

Because `docs/` is uploaded as the artifact root, `docs/index.html` becomes:

```text
https://masarray.github.io/DigSubAnalyzer/
```

It is not served as:

```text
https://masarray.github.io/DigSubAnalyzer/docs/
```
