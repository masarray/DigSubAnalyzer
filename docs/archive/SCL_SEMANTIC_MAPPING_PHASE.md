# SCL Semantic Mapping Phase

## Purpose

The analyzer must not treat SCL as a simple expected-vs-observed checklist. The correct order is:

1. Import SCL/CID/ICD/IID/SCD.
2. Detect SCL edition / namespace family.
3. Build an engineering model of IEDs, LDevices, DataSets, GSEControl, SampledValueControl, Communication addresses, and DataTypeTemplates.
4. Bind live SV/GOOSE streams to the SCL model.
5. Map payload order to DataSet entries.
6. Resolve signal references, FC, CDC, bType, type IDs, and enum types.
7. Only after semantic mapping is reliable, perform expected-vs-observed validation.

## Implemented in this phase

- Namespace-tolerant SCL parser using `XElement.Name.LocalName` so vendor namespaces and Edition 1 / 2 / 2.1 style files are not rejected prematurely.
- Edition detection based on SCL namespace family.
- IED, DataSet, FCDA, GSEControl, SampledValueControl, Communication/GSE/SMV Address parsing.
- DataTypeTemplates index for LNodeType, DOType, DAType, and EnumType.
- Type resolver for common `doName` / `daName` paths including nested DO/DA structures.
- Advanced workspace Load SCL / Clear SCL actions.
- Per-target Advanced engineering view appends SCL semantic match result when available.
- GOOSE inspector maps allData indexes to matched SCL DataSet entries.
- SV Advanced engineering view shows SCL payload element-to-signal mapping for the matched stream.

## Design rule

Do not silently auto-correct live SV phase order based on guesses. Use SCL mapping first. If SCL is unavailable, show phase-order suspicion as advisory only.

## Next steps

- Promote the SV semantic element map into the phasor/waveform channel mapper after scaling and quality pairing are validated.
- Add a conflict resolver for duplicated APPID/MAC/confRev/DataSet definitions.
- Add report/evidence export based on the semantic binding results.
