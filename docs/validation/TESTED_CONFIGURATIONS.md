# Tested Configurations

This matrix records explicit evidence. It is not a vendor-certification claim.

| Area | Configuration | Evidence level | Status |
| --- | --- | --- | --- |
| Build | Windows `windows-latest`, .NET 8 SDK | GitHub Actions build/test | Automated |
| Parser | Golden SV frames and malformed BER cases | xUnit regression tests | Automated |
| Runtime stability | Eight interleaved SV streams, exact 2/4/8-cycle windows, 65536 rollover, duplicate/gap handling, concurrent snapshot reads | Repeated `RuntimeStability` xUnit category with TRX artifacts | Automated |
| Runtime snapshot | Copied selected-stream identity, waveform, analog, phasor, shape, and diagnostics; atomic generation publication | Repeated `RuntimeArchitecture` xUnit category with TRX artifacts | Automated |
| Classic PCAP replay | PCAP 2.4, Ethernet link type 1, little/big endian, micro/nanosecond timestamps, truncated/oversized rejection | Repeated `RuntimeArchitecture` xUnit category with TRX artifacts | Automated |
| Replay isolation | Three interleaved SV streams replayed through the live raw analyzer entry point | Runtime architecture regression tests | Automated |
| Packaging | Windows x64 Inno Setup self-contained installer (primary), portable ZIP (optional); install/repair/offline manuals/uninstall | Windows candidate workflow + SHA256 smoke gates | Automated |
| Website | Static GitHub Pages source validation | Pages workflow | Automated |
| Live capture | Windows + Npcap + selected Ethernet adapter | Historical bench and current screenshot show adapter + one SV stream; fresh final candidate acceptance required | **Pending current final SHA** |
| Runtime soak | Historical v1.3.0-beta.2 60-minute live session, three streams, memory 141.6/144.5/142.1 MB | `V1.3.0_BETA2_FIELD_EVIDENCE.md` | PASS **historical only; pending v1.4.0 final candidate** |
| Timing | Windows/Npcap or recorded PCAP timestamps | Screening only | Not measurement-grade |
| PCAPNG replay | Not implemented in v1.4.0-beta.1 | No evidence | Not claimed |
| Multi-vendor SCL | SCD/ICD/CID examples and parser tests | Partial | Broader validation required |
| Hardware timestamping | Validated NIC/TAP timestamp path | None in public beta | Not claimed |

The 2026-10-19 same-session injector/analyzer screenshot shows close agreement for one 4I+4V 50 Hz configuration, including Ia +30°, Ib -90° and Ic +150°, but does not prove sustained resource usage or packet-by-packet equivalence. See [current v1.4.0 acceptance gates](V1.4.0_PUBLIC_RELEASE_ACCEPTANCE.md).

## Release evidence to record

For each public beta, record:

- Windows build used
- Npcap version
- adapter/capture-path or replay-source type
- publisher or sanitized replay source
- replay file format, link type, timestamp resolution, and endian variant
- SV rates and timebases exercised
- multi-stream count
- harmonic/shape scenarios
- 4000 and 65536 rollover behavior
- duplicate/gap/restart behavior
- duration of soak test
- memory start/peak/end observations
- UI freeze, disconnect, and stream cross-contamination counts
- immutable runtime generation and replay regression results
- known limitations

Use [`V1.3.0_BETA2_FIELD_EVIDENCE.md`](V1.3.0_BETA2_FIELD_EVIDENCE.md) for the maintained 60-minute live stability baseline. Add a new sanitized record when v1.4 replay is validated with a real field capture.

Sanitize all project, device, MAC/IP, site, customer, SCL, and capture identifiers before publishing evidence.
