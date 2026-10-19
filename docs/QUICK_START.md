# Quick Start — Process Bus Insight

Process Bus Insight is a receive-only IEC 61850 Process Bus analyzer for Windows. This guide explains the recommended Inno Setup installation (or optional portable ZIP) and a safe observation workflow.

## 1. Prepare the Windows machine

Recommended setup:

- Windows 10 or Windows 11 x64.
- A physical Ethernet adapter connected to a TAP, mirror port, or isolated test network.
- Npcap installed with normal raw packet capture support.
- Local admin rights if your Windows/Npcap policy requires elevated capture access.

Avoid these adapters for serious timing interpretation:

- Loopback adapters.
- Wi-Fi / Wi-Fi Direct.
- Hyper-V, VMware, VPN, or virtual bridge adapters.
- Unverified USB Ethernet adapters when timing evidence matters.

## 2. Download and install

1. Open [official GitHub Releases](https://github.com/masarray/DigSubAnalyzer/releases).
2. Download `ProcessBusInsight-vX.Y.Z-win-x64-setup.exe` and verify its SHA256 against `SHA256SUMS.txt`.
3. Close any prior running instance and run the Inno Setup installer. It installs into `C:\Program Files\Process Bus Insight` by default, creating a Start Menu shortcut and optional desktop shortcut.
4. For an upgrade, run the newer installer without uninstalling first; the stable application identity retains the previous install directory. Re-running the same installer can repair files.
5. The optional portable ZIP can still be extracted manually to `C:\Tools\ProcessBusInsight` when appropriate.

## 3. Run the app

Launch **Process Bus Insight** from the Start Menu, or run `ProcessBusInsight.exe` from the installed folder. **Quick Start.pdf**, **User Manual.pdf**, and **Install and Update.md** are available offline in the same folder; shortcuts to both PDFs are in the Start Menu.

The application is self-contained. Npcap is installed separately for live capture, never silently by this installer. If Npcap cannot load, the application shows `Npcap unavailable — capture disabled` and will not fabricate a capture-capable Ethernet adapter.

## 4. Start a safe capture workflow

1. Select the physical Ethernet adapter connected to the Process Bus traffic.
2. Start capture.
3. Confirm traffic appears in the event log.
4. Review SV stream discovery.
5. Review GOOSE publishers and event changes.
6. Review PTP timing context if present.
7. Load SCL when available and compare expected-vs-observed objects.
8. Copy evidence only after confirming adapter and capture-path confidence.

## 5. Evidence checklist

For a useful FAT/SAT troubleshooting screenshot or copied finding, include:

- Selected adapter raw device name.
- SV stream APPID, svID, VLAN, source MAC, and stream status.
- GOOSE publisher/control block and stNum/sqNum state when relevant.
- PTP transport/domain/grandmaster context when relevant.
- Missing sample / sequence / arrival timing status.
- Timing confidence label and timestamp source wording.
- Expected SCL fields and observed fields when SCL is loaded.

## 6. Timing caution

Arrival timing shown by the app is based on host/Npcap software timestamps. Treat it as screening evidence. Do not use it as certification-grade jitter proof unless the capture path is validated with hardware timestamping, TAP, or trusted timing equipment.
