# External IP and Provenance Review — 2026-07-15

## Scope

This review covers the current tracked repository, public documentation, release packaging, and visible contribution history for Process Bus Insight / DigSubAnalyzer.

## Observed repository boundary

- The project is a receive-only IEC 61850 Process Bus analyzer implemented in the repository's own source tree.
- Visible merged pull requests reviewed for the current line were submitted from the maintainer account.
- Runtime dependencies and separately installed prerequisites are identified in `THIRD_PARTY_NOTICES.md`.
- Public examples are expected to be synthetic, contributor-owned, or documented as authorized and sanitized.

## Independent-development rule

External software may be used only as a lawfully licensed black-box interoperability or packet-observation endpoint within the applicable authorization and license boundary.

Do not use unrelated external source, generated bindings, API composition, internal structures, tests, documentation wording, screenshots, icons, report layouts, UI composition, binaries, extracted resources, or confidential support material as implementation design material unless the project has documented rights to incorporate and relicense it.

## Data and fixture rule

Do not commit customer, employer, station, credential, restricted network, or production evidence. Real SCL, PCAP, screenshots, logs, or diagnostics require documented sharing rights and sanitization. Prefer synthetic fixtures that reproduce only the engineering condition under test.

## Findings

No external human code contributor was identified in the visible pull-request evidence reviewed for this migration. That supports, but does not conclusively prove, maintainer control over the current repository line.

Repository inspection cannot determine:

- employment or invention-assignment obligations;
- confidentiality or customer restrictions;
- rights in private or deleted artifacts;
- whether every historical input was independently created;
- trademark priority outside the repository; or
- rights required for a specific commercial deliverable.

## Required ongoing controls

- CLA affirmation and DCO sign-off for new contributions;
- provenance disclosure for third-party material;
- synthetic or documented authorized fixtures;
- required third-party notices and SBOM/dependency evidence where available;
- no external product affiliation or imitation claims; and
- transaction-specific legal review before a significant OEM or proprietary agreement.