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
- When a matching SCL GOOSE stream is available, the inspector maps `allData` index to the SCL DataSet entry order.
- SCL-backed rows now show engineering signal reference plus FC / CDC / bType alongside the raw MMS value type.
- The inspector shows whether the DataSet values are coming from SCL semantic mapping or generic typed decode.

## Still planned

- Semantic labels for common enumerations, breaker position, trip, alarm, and interlock signals.
- Dataset quality interpretation.
- Stronger handling for nested/structured GOOSE values and quality bit interpretation.
