# Licensing

## Current community edition

Revisions after the historical boundary on `main`, and release packages built from those revisions, are licensed **only** under:

```text
GPL-3.0-or-later
```

The GNU General Public License gives users permission to run, study, modify, and redistribute covered software subject to its terms, including corresponding-source obligations when conveying object code.

## Historical Apache-2.0 boundary

The final revision released under Apache-2.0 as the active project license is:

```text
85d43a0fe58a5888a9e8008c168ab76d2333ea87
```

It is preserved on:

```text
archive/apache-2.0-final
```

Revisions at or before that boundary, and historical binaries built from those revisions, retain the rights already granted under Apache-2.0. Those historical rights are not revoked. Later revisions and post-transition binaries are not offered as an Apache-or-GPL choice.

The project should also maintain a protected annotated tag named `license-boundary/apache-2.0-final` at the exact boundary commit. The commit SHA remains the controlling reference even if a branch or tag is unavailable.

## Commercial path

Organizations requiring proprietary integration, OEM or white-label distribution, closed-source redistribution, private branches, contractual support, training, warranty, or engineering services may discuss a separate commercial agreement with Ari Sulistiono for rights he controls.

`COMMERCIAL-LICENSE.md` is an invitation to negotiate. It is not itself an executed license and grants no additional rights.

Commercial rights can cover only material and rights controlled by the relevant granting party. Third-party components, contributor-owned material, standards, historical grants, and private obligations remain subject to their own terms.

## Contributions

New contributions require:

- affirmative acceptance of CLA Version 1.0 in `CONTRIBUTOR-LICENSE-AGREEMENT.md`;
- a Developer Certificate of Origin sign-off on every commit;
- authority to submit the material, including any required employer or organizational approval; and
- documented provenance for any third-party or non-synthetic fixture or asset.

## Packages and corresponding source

Post-transition portable packages must include:

```text
LICENSE.txt
NOTICE.txt
COMMERCIAL-LICENSE.md
COPYRIGHT.md
TRADEMARK.md
THIRD_PARTY_NOTICES.md
Licensing.md
Asset Provenance.md
SOURCE.md
sbom.cdx.json
```

`LICENSE.txt` must contain GNU GPL version 3. `SOURCE.md` must identify the exact built commit and an immutable source-archive URL. A historical Apache license file must not be included in a post-transition package in a way that suggests Apache-2.0 applies to the current binary.

## Scope limitation

Repository records document licensing intent and public release boundaries. They do not by themselves establish employer ownership, resolve invention-assignment or confidentiality duties, or determine rights in off-repository material. Significant commercial transactions should review the exact contributors, contracts, dependencies, trademarks, fixtures, assets, and deliverables involved.
