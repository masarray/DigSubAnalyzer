# PTP UDP Transport Support - Phase 2

This patch extends the raw passive timing layer so PTP can be observed not only as native Layer-2 IEEE 1588 Ethernet (`0x88F7`), but also as PTPv2 over UDP/IP.

## Supported PTP transports

- Native Ethernet PTP: EtherType `0x88F7`
- UDP/IPv4 PTP: UDP port `319` / `320`
- UDP/IPv6 PTP: UDP port `319` / `320` (direct UDP header, extension headers not decoded in this phase)

## Why this matters

PTPSync/PTPd commonly emits PTPv2 over UDP multicast, for example:

- `192.16.1.157 -> 224.0.1.129` Sync / Follow_Up / Announce
- `192.16.1.157 -> 224.0.0.107` Peer_Delay messages

The analyzer now unwraps:

```text
Ethernet -> IPv4/IPv6 -> UDP 319/320 -> PTPv2 common header
```

and sends the PTP payload to the same PTP parser used by Layer-2 PTP.

## Product positioning

Timing analysis remains software timestamp based unless a hardware timestamp capture path is validated. The product should continue to call this **PTP-aware timing screening**, not certified jitter measurement.
