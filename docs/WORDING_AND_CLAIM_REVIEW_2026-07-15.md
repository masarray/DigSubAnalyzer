# Public Wording and Claim Review — 2026-07-15

## Product description

Process Bus Insight / DigSubAnalyzer is described as receive-only engineering software for observing, decoding, replaying, visualizing, and comparing IEC 61850 Process Bus evidence on Windows.

## Evidence boundaries

Public wording must distinguish:

- SCL-configured expectations;
- frames observed at the selected capture or replay point;
- software decoding, calculations, and diagnostics; and
- external device reception, acceptance, time quality, or process action.

## Claims not established by the software

Do not claim or imply:

- formal IEC 61850 conformance or certification;
- calibrated or certification-grade timing and measurement;
- deterministic real-time operation;
- functional-safety or cybersecurity approval;
- universal interoperability;
- switching authority, equipment isolation, or site authorization;
- proof that an IED received, accepted, trusted, or acted on observed traffic; or
- equivalence to an approved test set, protection test system, or conformance platform.

Use scoped evidence terms such as implemented, observed, replayed, decoded, tested in the stated environment, provisional, unsupported, or not yet verified.

## Operational wording

The application is receive-only and does not publish SV or GOOSE or send IEC 61850 control commands. Nevertheless, capture must use an authorized TAP, mirror port, engineering switch, or other approved observation path. Software instructions cannot establish the safety or authorization of an external network.

## Licensing wording

Current community revisions and releases are `GPL-3.0-or-later` only. Historical Apache-2.0 references must identify the exact preserved boundary and must not imply a current dual-license choice. The commercial notice is an invitation to negotiate and grants no additional rights by itself.

## Review result

README, landing-page structured data, visible FAQ, package documentation, contribution guidance, and automated repository checks are updated by the migration to apply these boundaries.