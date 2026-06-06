# Quick Start — Process Bus Insight

Process Bus Insight is a receive-only IEC 61850 Process Bus analyzer for Windows. This guide helps you run the portable package and start a safe observation workflow.

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

## 2. Download and extract

1. Open the GitHub Releases page.
2. Download the latest `ProcessBusInsight-vX.Y.Z-win-x64-portable.zip`.
3. Download `SHA256SUMS.txt` if you want to verify file integrity.
4. Extract the ZIP to a local folder such as:

```text
C:\Tools\ProcessBusInsight
```

## 3. Run the app

Open the extracted folder and run:

```text
ProcessBusInsight.exe
```

The package may also include `Start-ProcessBusInsight.bat` as a convenience launcher.

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
