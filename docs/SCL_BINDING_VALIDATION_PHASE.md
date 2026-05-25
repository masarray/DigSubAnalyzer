# SCL Binding Validation Phase

This phase turns the SCL workspace into a live engineering-aware validator.

## Purpose

The analyzer must answer:

- Which SV/GOOSE streams are expected by the imported SCL files?
- Which expected streams are observed on the selected adapter?
- Which live streams are unexpected?
- How strong is the binding evidence?

## Implemented

- Binding matrix in the SCL workspace.
- Expected stream rows from imported SCL semantic catalog.
- Observed live SV/GOOSE comparison.
- Status categories: MATCHED, WEAK, MISSING, UNEXPECTED, MISMATCH, CONFLICT.
- Evidence summary from svID/goID, GoCBRef, APPID, DataSet, MAC, VLAN, priority, and confRev comparison.
- Expected-vs-observed side-by-side detail when a binding row is selected.
- Conflict screening for duplicate expected APPID and duplicated SCL control block definitions with different DataSet references.

## Product rules

- SCL is the engineering source of truth.
- Live binding must be explainable, not magical.
- Unexpected live traffic is as important as missing expected traffic.
- A weak match must be shown as weak, not pass.
- A mismatch must show the exact field that differs.
- A conflict must be visible before the user treats a binding as valid.
- This phase is still a validation matrix, not yet a full formal conformance report.

## Next steps

1. Add a richer per-field comparison table and conflict resolver workflow.
2. Apply SCL semantic labels to GOOSE allData values.
3. Apply SCL semantic labels to SV channel mapping.
4. Promote binding results into a dedicated validation dashboard.
5. Generate a FAT/SAT-style evidence report.
