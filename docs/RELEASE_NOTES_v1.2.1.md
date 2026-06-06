# Release Notes — v1.2.1-landing-and-release-fix

This maintenance release improves the public landing page and fixes the Windows portable release workflow.

## Highlights

- Premium GitHub Pages landing page refresh with cleaner typography, stronger screenshot presentation, app-icon branding, favicon, scroll reveal animation, and product trailer section.
- Windows portable release script now avoids command-line `AssemblyName` overrides so referenced projects keep their own assembly names during restore and publish.
- The release workflow pins the repository to the .NET 8 SDK through `global.json` and prints `dotnet --info` for easier CI diagnosis.
- The portable package remains a self-contained single-EXE app payload by default.

## Recommended package

Download the portable Windows package from GitHub Releases:

```text
ProcessBusInsight-v1.2.1-landing-and-release-fix-win-x64-portable.zip
SHA256SUMS.txt
```

Npcap is required on the target Windows machine and is not bundled with the application.
