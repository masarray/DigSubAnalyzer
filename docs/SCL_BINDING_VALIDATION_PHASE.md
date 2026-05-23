# SCL Binding Validation Phase

This phase turns the SCL workspace into a live engineering-aware validator.

## Purpose

The analyzer must answer:

- Which SV/GOOSE streams are expected by the imported SCL files?
- Which expected streams are observed on the selected adapter?
- Which live streams are unexpected?
- How strong is the binding evidence?

## Implemented first pass

- Binding matrix in the SCL workspace.
- Expected stream rows from imported SCL semantic catalog.
- Observed live SV/GOOSE comparison.
- Status categories: MATCHED, WEAK, MISSING, UNEXPECTED.
- Evidence summary from svID/goID, APPID, DataSet, MAC, and VLAN similarity.

## Product rules

- SCL is the engineering source of truth.
- Live binding must be explainable, not magical.
- Unexpected live traffic is as important as missing expected traffic.
- A weak match must be shown as weak, not pass.
- This phase is still a validation matrix, not yet a full formal conformance report.

## Next steps

1. Add conflict detection.
2. Add per-field comparison table.
3. Apply SCL semantic labels to GOOSE allData values.
4. Apply SCL semantic labels to SV channel mapping.
5. Generate a FAT/SAT-style evidence report.
