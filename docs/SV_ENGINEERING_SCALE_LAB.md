# SV Engineering Units — independent capture lab

Process Bus Insight is receive-only. It cannot prove physical A/V scaling from
IEC 61850 SV packet identity, raw payload width, APPID, svID or a waveform plot.

## Simple workflow (no SCL required)

1. Run the publisher, select the receiving Ethernet/Npcap adapter in Process
   Bus Insight, press **Run**, then select the *canonical* stream under SV Stream
   Explorer. The separate broadcast diagnostic mirror remains independent.
2. Check waveform and phase relationships immediately; this works in **Raw
   counts (uncalibrated)** mode. RMS and waveform axes indicate **counts**.
   P/Q/S/PF are unavailable rather than fictional MW/Mvar.
3. In **Metering > SV units**, opt in to **4I+4V nominal (1 mA / 10 mV per
   count)** *only* when that is the known mapping of the selected lab publisher.
   For example, raw 1000 corresponds to 1.000 A; raw 5774 corresponds to
   57.74 V. This is not independent calibration or a universal SV rule.
4. Check A/V RMS, 50 Hz, coherent phasors, waveform, P/Q/S and power factor.
   Try changing a single phase while the publisher runs; compare the
   timestamp-aligned evidence rather than screenshots taken at different times.
5. Use **Diagnostics > Copy Evidence**. Text includes per-stream SV identity,
   sample counters, decode and timing evidence, the selected engineering scale,
   the operator authority warning, RMS A/V/count values and P/Q/S/PF.
6. Select another stream: it defaults to **Raw counts** until explicitly
   configured. Return to the first stream to restore its own scaling choice.
   A fresh capture reset clears the analyzer's runtime state; select the
   nominal profile again when needed.

## Acceptance boundaries

- Known 4I+4V: `I=0.001 A/count`, `U=0.01 V/count`; voltage/current axes
  have separate autorange and do not accept fixed A/V limits while uncalibrated.
- One coherent selected-stream snapshot owns waveform, RMS, phasor and power.
- Pure power regression: three phases at 57.74 V, 1 A and 30 degree angle
  offset produce roughly 173.22 VA, 150 W and 86.61 var. Any raw-count,
  missing-phase or nonfinite input rejects physical power.
- Packet quality, sample continuity, VLAN visibility, Npcap capture drops,
  host arrival jitter and hardware PTP lock remain separate facts.
- Use locally held operator PCAP and physical ESP32-P4 to validate Windows
  candidate; do not commit private recordings or identifying site data.
- A successful waveform or power calculation does not prove calibration,
  hardware timestamp quality or that a real IED accepted SV.
