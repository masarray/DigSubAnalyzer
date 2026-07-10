# Security Policy

## Supported versions

The latest public-beta line and the default `main` branch receive security fixes. Older beta packages may be asked to upgrade before investigation.

## Reporting a vulnerability

Use a private GitHub security advisory:

`https://github.com/masarray/DigSubAnalyzer/security/advisories/new`

Do not publish exploit details, malicious captures, customer SCL files, or sensitive screenshots in a public issue.

Include the affected version/commit, Windows and Npcap environment, minimal reproduction steps, impact, and sanitized evidence. An initial acknowledgement is targeted within seven days; remediation timing depends on severity and reproducibility.

## In scope

- Unsafe parsing of crafted Ethernet, SV, GOOSE, PTP, BER, MMS, or SCL input
- Crashes, hangs, excessive allocation, or resource exhaustion from malformed traffic
- Cross-stream state contamination that can misrepresent engineering evidence
- Accidental capture, log, configuration, or project-data disclosure
- Release-package integrity and workflow-permission issues

## Out of scope

- Certification of timing measurement accuracy
- Unsupported or compromised Npcap/driver installations
- Vulnerabilities in third-party software not distributed by this repository
- Social engineering, denial-of-service against public infrastructure, or tests involving customer networks without authorization

## Data handling

Process Bus Insight is intended for local engineering use. Sanitize captures, SCL files, screenshots, MAC/IP addresses, device names, and project references before sharing them.
