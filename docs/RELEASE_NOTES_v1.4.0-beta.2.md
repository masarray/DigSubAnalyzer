# Process Bus Insight v1.4.0-beta.2

This beta establishes the post-transition licensing, provenance, package, and public-claim boundary for Process Bus Insight while preserving the receive-only runtime architecture introduced in v1.4.0-beta.1.

## Licensing

- Current source and this release are `GPL-3.0-or-later`.
- The final active Apache-2.0 revision is `85d43a0fe58a5888a9e8008c168ab76d2333ea87`, preserved on `archive/apache-2.0-final`.
- Historical v1.4.0-beta.1 and earlier grants remain effective for the revisions and artifacts to which they applied.
- Proprietary integration, OEM or white-label distribution, closed-source redistribution, private branches, and contractual services require a separate negotiated agreement.
- `COMMERCIAL-LICENSE.md` is an invitation to negotiate and grants no additional rights by itself.

## Repository and package hardening

- Added copyright, trademark, CLA, DCO, licensing-transition, provenance, and public-wording records.
- Added automated GPL and commercial-license boundary checks to the repository-health gate.
- Current portable packages include GPL, NOTICE, commercial licensing, copyright, trademark, third-party, and licensing-summary documents.
- Package verification rejects a stale Apache license presented as the current package license.
- Candidate packaging now runs for licensing branches.
- Release manifests identify the community license and historical boundary.

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