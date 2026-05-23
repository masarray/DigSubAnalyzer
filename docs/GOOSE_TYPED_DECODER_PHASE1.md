# GOOSE Typed Dataset Decoder - Phase 1

This update moves the GOOSE page from raw `allData` byte visibility to an IEDScout-style inspector model.

## Implemented

- Raw GOOSE `allData` BER/MMS Data decoding.
- Generic typed dataset entries without requiring SCL:
  - Boolean
  - BitString
  - Integer
  - Unsigned
  - Float
  - VisibleString / MMSString
  - OctetString
  - UtcTime
  - Structure / Array summary
- GOOSE event timeline now shows meaningful value summaries instead of `raw allData n bytes`.
- GOOSE detail panel now uses actual source/destination MAC and VLAN values from the Ethernet frame.
- Dataset value table now presents `Name / Type / Value` similar to a GOOSE inspector.
- State-change rows can show changed value summaries such as `Boolean 2: false -> true` when consecutive dataset values differ.

## Still planned

- SCL dataset mapping so generic names like `Boolean 2` become signal names such as `XCBR1.Pos.stVal`.
- Semantic labels for common enumerations, breaker position, trip, alarm, and interlock signals.
- Dataset quality interpretation.
- Expected-vs-observed validation against SCD/CID/ICD files.
