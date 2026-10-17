# Physical test quick start — DigSubAnalyzer / Process Bus Insight

This folder is included in the **Windows x64 release candidate ZIP** so the bench
does not need a .NET SDK. Run only on an authorized, isolated engineering process
bus. The analyzer is **receive-only** and cannot operate or protect an IED.

## 1. Run the desktop application

1. Extract the entire ZIP, not just the EXE, to a local folder.
2. Install Npcap separately with appropriate raw Ethernet capture rights.
3. Run `ProcessBusInsight.exe` in the package root.
4. Select the verified adapter connected to an independent TAP/SPAN (preserve
   802.1Q tags and multicast). Start capture and select the **canonical** SV
   stream, not the diagnostic mirror.
5. Load the actual deployed `.scd`/`.cid`/`.iid` via the SCL workflow.
6. In **Validation → Inspect PCAP**, select a local classic Ethernet PCAP
   recording. Read the short verdict; detailed evidence is in the report from
   the CLI below. For large captures progress/Cancel are supported.

If canonical VLAN multicast traffic is not visible but `AR_DIAG_SV1` is,
the result is **mirror-only**. This does not prove the injector dropped VLAN
or ignored SCL; verify TAP/SPAN and NIC filtering. PTP messages observed on
the NIC do **not** establish clock discipline or lock.

## 2. Screen the same wire recording with the bundled CLI

Open PowerShell in the extracted package directory. Replace file paths with
files collected on your **own local bench**:

```powershell
.\Bench\ProcessBus.InteropCli.exe --help
.\Bench\ProcessBus.InteropCli.exe --pcap "C:\lab\sv-ptp.pcap" --scl "C:\lab\deployed.scd" --max-frames 50000000 --json "C:\lab\interop-report.json"
```

The bundled CLI is a **self-contained Windows executable**. It parses classic
Ethernet PCAP, not PCAPNG. Convert PCAPNG to classic PCAP locally without
losing Ethernet/VLAN headers if necessary. A full 60-minute recording with
both canonical SV and a mirror may exceed 28 million SV packets alone
(at 4000 samples/s and nofASDU=1). Use the explicit frame limit or split
the PCAP into indexed windows; **CAPTURE_INCOMPLETE** must not be recorded as
a clean acceptance.

Exit codes: **0** screening completed with no failing configured comparison
(not IED conformance); **3** missing/ambiguous/conflicting/incomplete evidence;
**2** invalid or unsupported input. For a trustworthy SCL comparison always
supply `--scl`. The local JSON contains wire MAC and IED identities; do
**not** upload it or raw SCL/PCAP to a public issue.

## 3. Measure application load and physical truth separately

While the desktop app receives simultaneous 4000/4800-profile SV, diagnostic
mirror and L2 PTP, record local process metrics:

```powershell
.\Bench\measure-m4-bench.ps1 -DurationMinutes 60 -IntervalSeconds 5 -ProcessName ProcessBusInsight -OutputPath "C:\lab\m4-process-metrics.csv"
```

If PowerShell blocks scripts, follow your organization's signed-script policy;
do not turn off system-wide execution protection for this task. Collect
Npcap capture-driver/interface drops and ARStack device TX failure/missed-slot
telemetry separately; a zero `smpCnt` gap count does not prove zero wire loss.

Before any **physical acceptance** claim, independently verify:

- Canonical APPID/MAC/VLAN/PCP/svID/confRev/nofASDU/layout and 4000/4800
  cadence with the actual deployed SCL and applicable counter policy;
- `AR_DIAG_SV1` (broadcast, APPID 0x4F01) remains a separate debug stream;
- PTP Announce/Sync/Follow_Up/peer-delay and sync flags remain observed facts,
  not unmeasured lock or precision claims;
- 60-minute concurrent stability, driver drops, resource behavior and IED
  subscriber acceptance with qualified bench equipment.

The detailed step-by-step sign-off matrix is in
`Bench/M4_ARSTACK_INTEROP_ACCEPTANCE.md`. Record the candidate artifact run
ID and checksums with your evidence. This candidate is not a stable/certified
release and provides no switching, control or protection-action authority.
