# Release Notes - v1.2.6

Process Bus Insight v1.2.6 improves the download experience, portable package layout, and product landing page for engineers who want to try the tool quickly on a Windows machine.

## What's new

- Cleaner GitHub Pages landing page with smaller typography, smoother navigation, and a more polished product preview.
- Clickable screenshots with fullscreen preview, blurred background, and simple close behaviour.
- Updated screenshot gallery and trailer section for first-time users evaluating the tool.
- Refined FAQ for substation automation engineers preparing to use the app during FAT, SAT, commissioning, or lab troubleshooting.
- Portable release package now opens directly from `ProcessBusInsight.exe`.
- Release ZIP now includes `Quick Start.pdf` and `User Manual.pdf` for offline use.

## Download

Use the Windows x64 portable package from GitHub Releases:

```text
ProcessBusInsight-v1.2.6-win-x64-portable.zip
SHA256SUMS.txt
```

After extracting the ZIP, run:

```text
ProcessBusInsight.exe
```

## Included documents

- `Quick Start.pdf` - first-run checklist and safe capture setup.
- `User Manual.pdf` - practical guide for SV, GOOSE, PTP, SCL validation, evidence, and timing limitations.

## Runtime scope

Process Bus Insight is receive-only and raw-passive. It decodes observed SV, GOOSE, and PTP traffic and helps compare live traffic against SCL engineering expectation. It does not send control commands and does not replace certified timing or relay test equipment.

Npcap is required on the target machine and is not bundled in this repository.
