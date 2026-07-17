# Security Policy

## Supported versions

The latest post-transition public-beta line and the default `main` branch receive security fixes. Historical beta packages may be asked to upgrade before investigation.

## Reporting a vulnerability

Use a private GitHub security advisory:

`https://github.com/masarray/DigSubAnalyzer/security/advisories/new`

Do not publish exploit details, malicious captures, customer SCL files, credentials, or sensitive screenshots in a public issue.

Include the affected version and build commit from About, Windows and Npcap environment, minimal reproduction steps, impact, and sanitized evidence. An initial acknowledgement is targeted within seven days; remediation timing depends on severity, reproducibility, and maintainer capacity.

## In scope

- Unsafe parsing of crafted Ethernet, Sampled Values, GOOSE, PTP, BER, SCL, or supported PCAP input
- Crashes, hangs, excessive allocation, or resource exhaustion from malformed or adversarial traffic
- Cross-stream state contamination that can misrepresent engineering evidence
- Accidental capture, log, configuration, screenshot, source-offer, or project-data disclosure
- Release-package integrity, source/SBOM mismatch, signature, checksum, workflow-permission, or provenance issues
- Public wording that materially misstates the active license or package build identity

## Out of scope

- Certification of timing measurement accuracy
- Unsupported or compromised Npcap/driver installations
- Vulnerabilities in third-party software not distributed by this repository
- Social engineering, denial-of-service against public infrastructure, or tests involving customer or employer networks without authorization
- IEC 61850 control or MMS-client behavior, which is outside this receive-only product build

## Data handling

Process Bus Insight is intended for local engineering use. Sanitize captures, SCL files, screenshots, MAC/IP addresses, device names, credentials, and project references before sharing them. Prefer synthetic fixtures and use an authorized TAP, mirror port, or isolated engineering network.

## Release trust

A public post-transition release should provide a checksum, exact `SOURCE.md`, CycloneDX SBOM, release manifest, build attestation, and a valid Authenticode signature. Do not treat an unsigned local or pull-request candidate as an official signed release.
