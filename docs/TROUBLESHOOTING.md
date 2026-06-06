# Troubleshooting — Process Bus Insight

This guide covers common setup and field-use issues for the Windows portable package.

## No adapters appear

Possible causes:

- Npcap is not installed.
- Capture permission is blocked by Windows policy.
- The app is running without the required access rights.
- Npcap service/driver installation is incomplete.

Actions:

1. Install or repair Npcap.
2. Reboot the machine if the Npcap installer requests it.
3. Run the app again.
4. If your environment requires it, run as Administrator.

## Adapter appears but no traffic is visible

Check:

- The selected adapter is the physical NIC connected to the test network.
- The mirror/TAP port is actually forwarding SV/GOOSE/PTP frames.
- VLAN handling on the switch/mirror port is correct.
- The traffic is not only on a different NIC.
- Windows firewall or security tools are not blocking capture access.

A practical first check is to confirm the same adapter can see traffic in Wireshark.

## SV streams appear unstable

Common root causes:

- Wrong mirror port direction or incomplete VLAN mirroring.
- NIC/driver buffering or USB Ethernet batching.
- Publisher scheduling issue.
- Network congestion, duplicate frames, or out-of-order frames.
- Using a virtual/loopback/Wi-Fi adapter.

Use the app to separate:

- sequence continuity problems,
- missing samples,
- arrival timing excursions,
- capture path confidence,
- and PTP context.

## GOOSE publisher is visible but values look incomplete

Possible causes:

- The GOOSE payload uses a structure not fully decoded yet.
- SCL is not loaded, so semantic names are unavailable.
- DataSet order or confRev differs from the expected engineering file.

Actions:

1. Confirm APPID, goID/gocbRef, DataSet, confRev, and source MAC.
2. Load SCL when available.
3. Compare typed values against expected DataSet order.
4. Capture a screenshot and record the raw fields for follow-up.

## PTP is not shown

Check:

- PTP may be transported as Ethernet `0x88F7` or UDP port `319/320` depending on the network.
- The mirror/TAP port must include timing traffic.
- Some lab networks do not forward PTP to the same capture point.

PTP visibility is a context layer. The app does not become a PTP grandmaster and does not discipline the Windows clock.

## Timing evidence looks severe

Treat timing findings carefully:

- Normal Windows/Npcap timestamps are software based.
- USB adapters, virtual adapters, overloaded PCs, or poor mirror ports can create misleading timing symptoms.
- Use hardware timestamping, TAP, or trusted protocol analyzer for formal timing proof.

Use the app wording as screening evidence unless the capture path is externally validated.
