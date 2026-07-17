## Engineering intent

Describe the FAT/SAT, commissioning, interoperability, troubleshooting, legal, documentation, or repository-quality problem addressed by this change.

## What changed

- 

## Validation

- [ ] `dotnet restore .\ProcessBusSuite.sln`
- [ ] `dotnet build .\ProcessBusSuite.sln -c Release --no-restore`
- [ ] `dotnet test .\ProcessBusSuite.sln -c Release --no-build`
- [ ] `pwsh .\scripts\repository-health.ps1`
- [ ] `python .\scripts\generate-release-pdfs.py --check`
- [ ] Runtime smoke test on Windows, when runtime behavior changed
- [ ] Documentation/screenshots updated when user-visible behavior or claims changed
- [ ] Portable package, SOURCE.md, SBOM, checksum, and binary wording verification completed when packaging or legal content changed

## Product, claim, and evidence boundary

- [ ] Receive-only product boundary is preserved
- [ ] Timing language matches timestamp-source confidence
- [ ] Expected configuration, observed traffic, software interpretation, and external-device behavior remain distinct
- [ ] No claim implies formal conformance, calibration, deterministic timing, functional safety, cybersecurity approval, universal interoperability, switching authority, or IED acceptance proof
- [ ] No customer, employer, station, device, credential, MAC/IP, capture, SCL, or project-sensitive data is included
- [ ] Any fixture, screenshot, logo, icon, font, or image is synthetic, contributor-owned, or documented as authorized and sanitized
- [ ] No unrelated proprietary code, tests, wording, screenshot, report, asset, or UI design was copied or mechanically translated
- [ ] No `bin`, `obj`, `artifacts`, logs, captures, or local settings are committed

## Contribution licensing

- [ ] I have read and affirmatively accept CONTRIBUTOR-LICENSE-AGREEMENT.md (CLA Version 1.0, effective 2026-07-17)
- [ ] I have the legal right and any required employer or organizational authorization to submit this contribution
- [ ] Every commit includes `Signed-off-by: Name <email>` under the DCO
- [ ] Any third-party material is identified with its license and provenance

## Compatibility / migration notes

None, or describe them here.
