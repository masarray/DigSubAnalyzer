# Security Policy

## Supported versions

The public beta branch is the active supported line for security reports and responsible disclosure.

## Reporting a vulnerability

Please open a private security advisory on GitHub when available, or contact the repository owner through the public GitHub profile if private advisories are not enabled.

Do not publish exploit details before the issue has been reviewed.

## Scope

Relevant security concerns include:

- Unsafe parsing of crafted SCL or network input.
- Crashes triggered by malformed SV, GOOSE, PTP, or Ethernet frames.
- Accidental exposure of local capture files, logs, or project data.
- Release package integrity issues.

## Out of scope

- Certification of timing measurement accuracy.
- Issues caused by unsupported Npcap/driver installation.
- Vulnerabilities in third-party software not distributed by this repository.

## Data handling

The application is intended for local engineering use. Avoid sharing captures, screenshots, or SCL files that contain customer, site, or project-sensitive information unless they have been sanitized.
