# Tested Configurations

This matrix records explicit evidence. It is not a vendor-certification claim.

| Area | Configuration | Evidence level | Status |
| --- | --- | --- | --- |
| Build | Windows `windows-latest`, .NET 8 SDK | GitHub Actions build/test | Automated |
| Parser | Golden SV frames and malformed BER cases | xUnit regression tests | Automated |
| Runtime stability | Eight interleaved SV streams, exact 2/4/8-cycle windows, 65536 rollover, duplicate/gap handling, concurrent snapshot reads | Repeated `RuntimeStability` xUnit category with TRX artifacts | Automated |
| Packaging | Windows x64 self-contained single-file ZIP | Release workflow and package verification script | Automated |
| Website | Static GitHub Pages source validation | Pages workflow | Automated |
| Live capture | Windows + Npcap + selected Ethernet adapter | Manual smoke test required | Maintainer validation |
| Runtime soak | 30–60 minute live/replay session with memory and UI observations | `V1.3.0_BETA2_FIELD_EVIDENCE.md` record | Required before beta.2 release |
| Timing | Windows/Npcap software timestamps | Screening only | Not measurement-grade |
| Multi-vendor SCL | SCD/ICD/CID examples and parser tests | Partial | Broader validation required |
| Hardware timestamping | Validated NIC/TAP timestamp path | None in public beta | Not claimed |

## Release evidence to record

For each public beta, record:

- Windows build used
- Npcap version
- adapter/capture-path type
- publisher or replay source
- SV rates and timebases exercised
- multi-stream count
- harmonic/shape scenarios
- 4000 and 65536 rollover behavior
- duplicate/gap/restart behavior
- duration of soak test
- memory start/peak/end observations
- UI freeze, disconnect, and stream cross-contamination counts
- known limitations

Use [`V1.3.0_BETA2_FIELD_EVIDENCE.md`](V1.3.0_BETA2_FIELD_EVIDENCE.md) for the maintained beta.2 record.

Sanitize all project, device, MAC/IP, site, and customer identifiers before publishing evidence.
