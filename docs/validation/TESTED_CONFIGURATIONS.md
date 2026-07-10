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
| Packaging | Windows x64 self-contained single-file ZIP | Candidate/release workflow and package verification script | Automated |
| Website | Static GitHub Pages source validation | Pages workflow | Automated |
| Live capture | Windows + Npcap + selected Ethernet adapter | Maintainer smoke/soak evidence | Maintainer validation |
| Runtime soak | 60-minute live session, three simultaneous streams, memory 141.6 MB start / 144.5 MB peak / 142.1 MB end, zero freeze/disconnect/cross-stream mixing | `V1.3.0_BETA2_FIELD_EVIDENCE.md` | PASS |
| Timing | Windows/Npcap or recorded PCAP timestamps | Screening only | Not measurement-grade |
| PCAPNG replay | Not implemented in v1.4.0-beta.1 | No evidence | Not claimed |
| Multi-vendor SCL | SCD/ICD/CID examples and parser tests | Partial | Broader validation required |
| Hardware timestamping | Validated NIC/TAP timestamp path | None in public beta | Not claimed |

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
