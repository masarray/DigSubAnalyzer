# Release Notes — v1.2.0-public-beta

This public beta hardens Process Bus Insight as a user-facing open-source Windows tool for IEC 61850 Process Bus visibility.

## Highlights

- User-facing README with clear download, run, build, documentation, and contribution guidance.
- SEO-ready GitHub Pages landing page with screenshots, FAQ, social preview metadata, and structured data.
- Windows portable release automation for GitHub Actions with a self-contained single-EXE app payload.
- Local PowerShell scripts to publish and verify the portable package.
- GitHub repository setup script for description, homepage, topics, and repository feature settings.
- Documentation for quick start, troubleshooting, validation matrix, deployment, release packaging, roadmap, security, and contribution flow.

## Runtime scope

Process Bus Insight remains receive-only and raw-passive. It decodes observed SV, GOOSE, and PTP traffic and helps compare live traffic against SCL engineering expectation. It does not send control commands or claim certified timing proof from normal host/Npcap timestamps.

## Recommended package

Download the portable Windows package from GitHub Releases:

```text
ProcessBusInsight-v1.2.0-public-beta-win-x64-portable.zip
SHA256SUMS.txt
```

Npcap is required on the target machine and is not bundled in this repository.
