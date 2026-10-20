# Installation and updates — Process Bus Insight

## Standard Windows installation (recommended)

Download the latest **ProcessBusInsight-v...-win-x64-setup.exe** from the official project's GitHub Releases. Run the Inno Setup installer as an administrator. Choose the destination (default: `C:\Program Files\Process Bus Insight`). A Start Menu entry is created; the desktop shortcut is optional.

The installer places **User Manual.pdf**, **Quick Start.pdf**, this guide, the executable, license notices and read-only Bench tools in that same program folder. You can open either PDF without an internet connection. The Start Menu also contains a direct **User Manual (PDF)** shortcut.

**Npcap is a separate prerequisite for live Ethernet capture**, not part of this installer. Offline PCAP analysis can still be used without Npcap. If Npcap is not detected the setup shows guidance rather than installing an unknown driver or blocking offline operation.

## Update or repair an installed copy

1. Stop any live capture and close Process Bus Insight (allow the application to finish its normal shutdown).
2. Download a newer official setup EXE, verify the published SHA256, and run it.
3. Inno Setup uses the same application ID and remembers the previous installation directory and shortcut options. No prior uninstall is normally necessary.
4. The installer detects files in use using the Windows Restart Manager; do not forcibly replace files while the analyzer is running.
5. Re-running the same version can repair damaged or missing installed files.

**There is no background or in-app automatic update claim**. Updating is deliberately operator-initiated; no capture is started automatically and no live traffic or customer information is sent to the update service. AppUpdatesURL points at the official releases page for manually obtaining newer versions.

## Uninstall, user data and troubleshooting

Use Windows Settings > Installed apps > Process Bus Insight > Uninstall, or the Start Menu uninstall shortcut. The installer removes its deployed binaries/docs/shortcuts, but intentionally has no broad deletion rule for unrelated user captures, SCL files, logs or profiles. Back up any data you previously placed inside the program directory before uninstalling; the recommended location for user data is outside `Program Files`.

The official community build is currently **unsigned**. Windows SmartScreen may show a publisher warning; do not treat that as proof of malware, and do not bypass security policy. Verify origin and SHA256 before deciding to run it. A future signed build would require a real code-signing certificate and verified CI provenance.

If installation fails, locate the Inno installer log (setup logging is enabled) and provide sanitized error details without sharing customer identifiers or raw captures. An installation smoke test on the CI runner is useful engineering evidence, not proof for all Windows versions, antivirus products and Npcap configurations.

For source builds and portable use, see the repository's release-packaging documentation.
