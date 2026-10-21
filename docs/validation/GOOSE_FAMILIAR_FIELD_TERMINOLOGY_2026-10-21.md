# GOOSE Message Terminology — Field Acceptance

## Purpose

Use widely recognized GOOSE capture and protocol field terminology in the GOOSE workspace while retaining the existing application layout, stable publisher selection, compact search and three independent Inspector sections. All terms describe actual received evidence, not assumed switching states.

## Capture table

| Column | Evidence |
| --- | --- |
| Time | Local receive time from the actual frame timestamp (seven fractional digits where available) |
| Relative time | Seconds since the first observed GOOSE history event after capture start/reset; **not** the time gap to the preceding event |
| Source | Source Ethernet MAC address |
| Destination | Destination Ethernet MAC address |
| Description | Original GOOSE control-block reference (GoCBRef), with full text in a tooltip |

The small `G` marker denotes a GOOSE frame; its tooltip retains the event distinction (first seen, state change, repeated). The repeated-message filter still controls which events are visible, not which frames are received. The receive count and event count remain separate.

## Details

- Control Block reference, Application ID, GOOSE ID, Status number (stNum), Sequence number (sqNum), Entry time.
- Destination MAC address, Source MAC address, DataSet reference, VLAN ID, VLAN priority, Needs commissioning, Configuration revision, Simulation/Test, Time allowed to live (ms).
- Data: Name and Value columns. When no valid SCL identity is available, a received MMS BitString is named **Bitstring N** only as an ordinal/type label, not as an IEC 61850 object identifier. Other recognized wire types likewise may use type/ordinal labels.

## Operator contract

- Dark theme, 8-DIP gutters, scrollable publisher list, one Material-style search/clear field and independent Message/Network/Data disclosure controls remain in place.
- DataSet interpretation does **not** infer a breaker position from an unmapped bit pattern. Verifiable SCL matching remains a prerequisite to semantic values and quality.
- Historical event selection remains distinct from live publisher selection. **Show Latest** returns to live evidence. The Inspector displays the selected message snapshot coherently.
- No external product/provider names are introduced in source, commit history, public PR or validation notes.

## Automated and physical checks

Automated: exact-SHA Windows compile, full tests, 8-DIP geometry and terminology contract, runtime architecture/stability, CodeQL, dependency analysis and candidate Inno install/repair smoke.

Physical: inspect 20+ active GOOSE sources, select two different messages, compare Source/Destination with a verified Ethernet capture, ensure Relative time is monotonic and reset with capture clearing, inspect all detail labels without clipping at 100%/125%/150% scaling, import matching SCL for genuine values and test raw Bitstring fallback without SCL.

Green CI is not a substitute for the final physical reading/interaction test.
