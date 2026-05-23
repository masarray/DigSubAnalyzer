# SCL Engineering Workspace Phase

This phase promotes SCL import from a small Advanced/debug panel into a first-class engineering workspace.

## Purpose

The analyzer must become engineering-aware before it becomes a commissioning validator. SCL is used to understand:

- which IEDs are present,
- which SV and GOOSE control blocks are expected,
- the transport identity of each stream,
- DataSet entry order,
- signal names,
- FC / CDC / bType information,
- which live traffic can be matched to engineering context.

## Implemented in this phase

- New `SCL` workspace tab.
- Multi-file SCL import.
- Imported document cards.
- IED cards.
- SV / GOOSE semantic stream catalog.
- Selected stream semantic detail panel.
- DataSet entry order table.
- ROADMAP and AGENTS updated to keep the project SCL-first.

## Design rule

SCL is not a raw/debug function. It is the semantic layer for SV, GOOSE, diagnostics, expected-vs-observed validation, and future reports.

## Next phase

Use the SCL stream catalog to:

1. label GOOSE typed values with semantic FCDA names,
2. build per-stream SV channel mapping,
3. show binding score and mismatch evidence,
4. detect conflicts across multiple imported SCL documents,
5. then implement expected-vs-observed validation.
