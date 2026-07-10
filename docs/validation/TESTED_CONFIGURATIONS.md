# Tested Configurations

This matrix records explicit evidence. It is not a vendor-certification claim.

| Area | Configuration | Evidence level | Status |
| --- | --- | --- | --- |
| Build | Windows `windows-latest`, .NET 8 SDK | GitHub Actions build/test | Automated |
| Parser | Golden SV frames and malformed BER cases | xUnit regression tests | Automated |
| Packaging | Windows x64 self-contained single-file ZIP | Release workflow and package verification script | Automated |
| Website | Static GitHub Pages source validation | Pages workflow | Automated |
| Live capture | Windows + Npcap + selected Ethernet adapter | Manual smoke test required | Maintainer validation |
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
- rollover/restart behavior
- duration of soak test
- known limitations

Sanitize all project, device, MAC/IP, site, and customer identifiers before publishing evidence.
