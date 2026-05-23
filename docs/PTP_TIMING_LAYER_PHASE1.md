# PTP Timing Layer - Phase 1

This phase adds a passive PTP visibility layer to the raw process-bus analyzer.

## What changed

- The raw capture BPF now includes EtherType `0x88F7` for IEEE 1588/PTP frames.
- The raw decoder can classify SV, GOOSE, and PTP frames from the same Ethernet capture path.
- A passive PTP parser decodes the common PTPv2 header and Announce information.
- The analyzer tracks PTP message presence, Grandmaster identity, domain, clock class, clock accuracy, steps removed, Sync/Announce/Follow_Up rates, and Grandmaster changes.
- The diagnostics UI now separates SV arrival timing variation from PTP timing context.

## Engineering position

The application does not claim to be a PTP clock, Grandmaster, slave clock, or certified timing meter. PTP is used as timing context and confidence gating for SV communication timing symptoms.

Timing language should remain:

- **Arrival timing variation** for SV packet-arrival deviation observed at the analyzer.
- **Arrival timing excursion** for deviations crossing the configured threshold, currently 300 us.
- **Timing confidence** for the reliability class of timing conclusions.

## Confidence model

- **LOW**: no PTP observed, virtual/loopback/wireless adapter, or arrival-only timestamp path.
- **SCREENING**: PTP observed on physical Ethernet with software/Npcap timestamp.
- **HIGH**: reserved for future hardware timestamp validated capture path.

## Next recommended phase

Phase 2 should add a dedicated Timing / PTP workspace and deeper health rules:

- Announce timeout threshold by logMessageInterval.
- Sync timeout threshold by logMessageInterval.
- Domain mismatch detection against expected profile/session setting.
- Multiple Grandmaster candidate detection.
- PTP/SV correlation messages.
- PCAP playback test set for PTP parser validation.
