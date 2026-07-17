# Process Bus Insight v1.4.0-beta.2

This beta establishes the post-transition licensing, provenance, package, and public-claim boundary for Process Bus Insight while preserving the receive-only runtime architecture introduced in v1.4.0-beta.1.

## Licensing

- Current source and this release are `GPL-3.0-or-later`.
- The final active Apache-2.0 revision is `85d43a0fe58a5888a9e8008c168ab76d2333ea87`, preserved on `archive/apache-2.0-final` and the annotated boundary tag.
- Historical v1.4.0-beta.1 and earlier grants remain effective for the revisions and artifacts to which they applied.
- Proprietary integration, OEM or white-label distribution, closed-source redistribution, private branches, and contractual services require a separate negotiated agreement.
- `COMMERCIAL-LICENSE.md` is an invitation to negotiate and grants no additional rights by itself.

## Release integrity

- The About dialog reads the informational version and build commit from the actual executable instead of presenting stale hard-coded values.
- Quick Start and User Manual PDFs are generated deterministically and identify GPL-3.0-or-later.
- `SOURCE.md` binds the package to its exact built commit, source-head commit, tested merge commit, source ref, and immutable source archive.
- A CycloneDX 1.5 SBOM records the application, .NET runtime, Windows Desktop runtime, and separately installed Npcap prerequisite.
- The package verifier checks license files, source offer, SBOM, EXE version, embedded wording, required PDFs, and optional Authenticode status.
- Public release publication is blocked until the Windows signing certificate secrets are configured.
- Release ZIP and SBOM provenance are attested through GitHub Actions.
- GitHub Pages changes are validated on pull requests and smoke-tested after deployment.

## Repository and governance hardening

- Added copyright, trademark, CLA, DCO, transition, provenance, asset-provenance, and public-wording records.
- Added automated GPL and commercial-license boundary checks to the repository-health gate.
- CLA Version 1.0 acceptance and DCO sign-off are verified for pull requests.
- Current portable packages include GPL, NOTICE, commercial licensing, copyright, trademark, third-party, asset-provenance, licensing-summary, source-offer, and SBOM documents.
- Candidate and release manifests distinguish the source-head, tested merge, and built commits.
- Release assets are not overwritten under an existing historical tag.

## Engineering scope retained

- Receive-only SV, GOOSE, PTP, SCL, and PCAP analysis.
- Immutable selected-stream runtime generations.
- Classic Ethernet PCAP replay through the same raw decoder/analyzer path used by live capture.
- No SV/GOOSE publishing and no IEC 61850 control-command path.

## Important limitations

- This remains a public beta, not a certified protection, conformance, timing, calibration, functional-safety, or cybersecurity platform.
- Windows/Npcap and replay timestamps remain engineering screening evidence unless the capture path is independently validated.
- Observed traffic and software interpretation do not prove that an external IED received, accepted, trusted, or acted on the traffic.
- Npcap is installed separately for live capture.
- A pull-request candidate may be unsigned. Only a public release that passes the signing gate should be described as the signed official package.
