# M4 — ARStack Injector ↔ DigSubAnalyzer interoperability acceptance

Date: 17 October 2026  
Repositories: `masarray/DigSubAnalyzer` (consumer, **receive-only**) and
`masarray/arstack61850` (ESP32-P4 SMV Injector).

## Three different evidence levels

1. **Software replay gate (automated):** anonymous synthetic PCAPs and SCL-intent
   fixtures exercise the **same production parser** used by live reception.
   This demonstrates decoder behavior but not a physical process bus.
2. **Independent observed-wire gate (operator bench):** capture on an independent
   TAP or correctly configured SPAN port that preserves multicast and 802.1Q
   tags. Compare observed frames with the actually deployed SCL profile.
3. **Device/IED gate (external equipment):** verify subscriber/IED acquisition,
   quality, synchronization and sustained operation with equipment-approved
   tools. DigSubAnalyzer alone cannot certify relay subscription, functional
   safety, protection action or hardware PTP synchronization.

The M4 software utility intentionally reports **WIRE_FIELDS_MATCH** instead
of “compliant”, “certified”, “locked” or “accepted by IED”.

## Run the offline acceptance inspector

Windows with .NET 8 SDK installed:

```powershell
dotnet build .\ProcessBusSuite.sln -c Release
dotnet test .\ProcessBusSuite.sln -c Release --no-build --filter Category=M4Interop
dotnet run --project .\tools\ProcessBus.InteropCli -c Release -- --pcap C:\lab\arstack-canonical.pcap --scl C:\lab\current-profile.scd --max-frames 20000000 --json C:\lab\interop-report.json
```

The tool supports classic Ethernet PCAP 2.4 (microsecond/nanosecond). If the
capture application creates PCAPNG, export/copy the packet recording as
**classic PCAP** with full Ethernet headers before inspecting it.

`--scl` is optional for wire fingerprint only; mandatory for automated
expected-vs-observed verdicts. The CLI prints a short operator summary,
keeps raw details in optional local JSON, and returns:

- **0**: screening executed and no specified SCL comparison failed; **not**
  certification and **not** proof of capture-path visibility. For a capture
  without SCL, there are no configured fields to validate.
- **3**: observed/configured mismatch, unresolved SCL identity or expected
  stream not observed; this includes **MIRROR_ONLY**.
- **2**: malformed arguments, unreadable/unsupported PCAP, or no useful
  successfully decoded traffic.

Bench files, identities, MAC addresses, PCAP, JSON logs and original SCL stay
on the authorized **local machine**; do not push them to GitHub.

### Verdicts

| Verdict | Interpretation | Operator action |
| --- | --- | --- |
| `WIRE_FIELDS_MATCH` | Visible fields match configured SCL stream | Verify TAP/SPAN provenance and independent IED behavior |
| `MIRROR_ONLY` | Only ARStack diagnostic broadcast identified | Fix capture visibility; do not infer canonical multicast/VLAN |
| `NOT_OBSERVED` | Configured svID absent | Check VLAN membership, multicast filtering, SPAN/TAP and NIC |
| `CONFLICT` | APPID/MAC/VID/PCP/confRev/nofASDU differs | Inspect deployed SCL and exact wire identity; do not normalize away |
| `INCOMPLETE_INTENT` | Some observed fields match but SCL lacks authority | Load the deployed full SCL/profile; do not auto-fill defaults |
| `UNRESOLVED` | No trustworthy SCL svID | Correct/resolve engineering intent before claiming match |

Capture **VLAN absence on a Windows USB-NIC is not proof of untagged wire TX**.
A mirrored `AR_DIAG_SV1`, APPID `0x4F01`, broadcast/untagged stream is diagnostic
evidence only, always separate from the canonical SCL stream.

## Physical bench procedure

1. Record ARStack Injector firmware build/manifest, selected immutable profile,
   source identity, configured DataSet order, 4I+4V payload authority, APPID,
   multicast MAC, VLAN ID/PCP, confRev, nofASDU, sample cadence, smpCnt modulus
   policy and intended `smpSynch`. The SCL is **configured intent**, not
   automatically the deployed firmware identity.
2. Connect a trusted independent TAP or SPAN that demonstrably retains VLAN
   tags, **without trusting a diagnostic mirror as a canonical substitute**.
   Capture timestamp source, precision, NIC/driver version and driver drops.
3. With an ARStack `LAB_SOURCE` release-candidate profile, observe L2 PTPv2,
   domain 0, Announce ~1/s, two-step Sync ~4/s, matching Follow_Up and peer
   delay response **when requested**. clockClass 248 and clockAccuracy Unknown
   do **not** constitute external UTC traceability; AUTO `smpSynch=0` remains
   truthful for undisciplined source mode.
4. With the same capture path, observe independent canonical SV and diagnostic
   mirror identities and intentionally verify **different** APPID, destination
   MAC, VLAN visibility and counters. Check `nofASDU`, channel layout,
   instMag/q quality and `smpCnt` continuity for the selected profile.
5. Test nominal **4000 and 4800 samples/s**, including appropriate
   profile-validated counter rollover, TEST/bad-quality stimuli, stream restart
   and multi-source isolation. A PCAP's host-arrival jitter is
   screening only, not on-wire timestamp quality.
6. Run mixed SV/PTP for at least **60 minutes** per acceptance scenario.
   A 4000 sample/s stream produces ~14.4m SV frames in 60 minutes if
   `nofASDU=1`; canonical plus mirror doubles the SV frames. Ensure
   `--max-frames` covers all traffic, or split recordings into explicitly
   indexed windows and inspect each.
7. Record capture-driver counters alongside publisher firmware telemetry
   (TX failure, missed slots and timestamp quality). A zero sequence-gap result
   on the reader does **not** imply an independent NIC dropped zero frames.
8. Independently verify receiving IED/subscriber behavior. A matched stream
   in DigSubAnalyzer is not proof the IED subscribed or acted on samples.

## Performance and UX acceptance

While the live analyzer displays mixed traffic, record every 5 seconds:

```powershell
.\\scripts\\measure-m4-bench.ps1 -DurationMinutes 60 -IntervalSeconds 5 -OutputPath C:\\lab\\m4-local-metrics.csv
```

This is a local process CPU/working-set sampler, **not** a substitute for
`dotnet-counters`, native Npcap driver statistics or hardware wire timestamps.


- CPU utilization and process memory/working set;
- .NET allocation rate and GC collections (`dotnet-counters` or equivalent);
- NIC driver/interface drops where supported;
- SV sample/counter anomalies, rejected frames and identity counts;
- selection-to-coherent-snapshot latency and scope redraw responsiveness.

Inspect the main workspace for these user-facing invariants:

- changing selected SV publisher never carries waveform, RMS, phasor or
  engineering scaling from the previous publisher;
- TEST and invalid quality are labeled; invalid data never appears as
  valid calibrated measurements;
- canonical and mirror traffic remain distinct;
- PTP observation, pairing, domain and freshness are not misrepresented as
  servo LOCKED;
- the one-line status remains calm, and detailed raw evidence lives under
  Timing/Diagnostics/Advanced, not in an overwhelming initial screen.

There is no synthetic CPU/heap reduction percentage quoted here. Measure
**before/after** on the same hardware/NIC/driver/traffic scenario and record
absolute values and drop counters. Without such measurements, resource
optimization is implementation-level, not performance-certified.

## Release gate and signed-off evidence

The engineering reviewer should retain, locally or in an authorized evidence
store: sanitizer-approved capture checksum, SCL version, firmware manifest,
analyzer commit/build, report JSON, port/TAP diagram and VLAN settings,
NIC/drop counters, 60-minute performance measurements and IED independent
results. Publish only sanitized summaries.

M4 cannot be called **physically complete** without this independently
collected evidence. The automated replay suite is suitable for a software
checkpoint and for detecting compatibility regressions.
