# Process Bus Insight v1.2.7

This release keeps the Windows portable package clean and improves release reliability.

## Included in this release

- Single executable Windows package for easier evaluation.
- Quick Start PDF and User Manual PDF included at the package root.
- Cleaner packaging validation for release documentation.
- Git ignore rules updated so public PDF documentation is committed while local generated reports remain ignored.

## Download

Download the Windows x64 portable ZIP from GitHub Releases, extract it to a local folder, and run `ProcessBusInsight.exe`.

Npcap is required when capturing live IEC 61850 traffic from a physical adapter.

## Notes for timing evidence

Windows and Npcap timestamps are useful for screening and troubleshooting, but certification-grade timing proof still needs validated timestamping equipment or trusted test tools.
