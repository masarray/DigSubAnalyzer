# User Manual - Process Bus Insight

Process Bus Insight is a receive-only Windows analyzer for IEC 61850 Process Bus visibility. It helps substation automation engineers observe Sampled Values, GOOSE, PTP timing context, and SCL expected-vs-observed validation during FAT, SAT, commissioning, and troubleshooting.

## Product boundary

- Receive-only and raw-passive.
- Does not send IEC 61850 control commands.
- Does not publish GOOSE or Sampled Values.
- Does not replace certified relay test sets or timing instruments.
- Uses Windows/Npcap capture timestamps for screening and troubleshooting.

## Before you start

Use a safe capture path such as a TAP, mirror port, or isolated engineering test switch. Avoid connecting an unverified PC directly into a protection-critical path. Install Npcap when raw Ethernet frame capture is required.

## Running the portable package

1. Download the latest Windows x64 portable ZIP from GitHub Releases.
2. Extract it to a local folder, for example `C:\Tools\ProcessBusInsight`.
3. Run `ProcessBusInsight.exe`.
4. Select the physical Ethernet adapter connected to the Process Bus traffic.
5. Start capture.

## Main workflow

1. Confirm the selected adapter is correct.
2. Observe live Sampled Values streams.
3. Inspect GOOSE publishers and event changes.
4. Check PTP traffic presence and timing context when available.
5. Load SCL when available.
6. Compare expected objects against observed live traffic.
7. Copy findings only after checking adapter, timestamp, and capture path confidence.

## Sampled Values view

Use the SV workspace to check stream identity, APPID, svID, VLAN, source MAC, continuity counters, waveform, phasor, and metering context. Treat missing stream or mismatch indications as investigation leads, not final protection conclusions until the network capture path is confirmed.

## GOOSE inspector

Use the GOOSE workspace to inspect publisher identity, state changes, stNum, sqNum, typed dataset values, and event history. This is useful when troubleshooting interlocking, trip, alarm, or binary status behaviour during testing.

## PTP timing context

Use PTP visibility to confirm timing traffic presence, transport, domain, and timing source context. Timing results from normal Windows capture are useful for screening, but not for certification-grade jitter proof.

## SCL validation

Load SCD, ICD, or CID files when engineering files are available. The app can compare expected APPID, VLAN, MAC, DataSet, confRev, and stream identity against observed traffic. This helps identify missing, unexpected, weak, mismatched, or conflicted objects.

## Troubleshooting no traffic

Common causes:

- Wrong adapter selected.
- Npcap is not installed or capture permission is restricted.
- Switch mirror port is not configured correctly.
- VLAN traffic is not forwarded to the capture port.
- PC is connected to the wrong network segment.
- Traffic is not actually present from the publisher.

## Evidence guidance

Good FAT/SAT evidence should include adapter name, capture path, APPID, VLAN, source MAC, svID or GOOSE identity, timing context, SCL expected field, observed field, and confidence wording. Avoid overclaiming timing performance from software timestamps.

## Safety note

Process Bus Insight is a visibility tool. Always follow site safety rules, commissioning procedures, protection engineering sign-off, and customer test requirements.
