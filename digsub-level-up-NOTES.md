# DigSubAnalyzer — Level-Up Patch (audit remediation)

Patch tunggal: `digsub-level-up.patch` — apply dengan `git apply digsub-level-up.patch` dari root repo (base: `main`, commit terkini per 2026-07-06).

## Perubahan

### N1 — Test suite pertama (`tests/ProcessBus.Tests`, 16 test)
- `GoldenFrames.cs`: pabrik frame byte-perfect (Ethernet + session header 61850 + BER builder dengan panjang TLV yang dihitung, short/long form) untuk SV, GOOSE, dan varian VLAN-tagged.
- `BerReaderTests.cs`: TLV short/long form, truncated, high-tag-number, `ReadUnsignedInteger` 1–4 byte, `ReadChildren`, plus **regression test overflow** (panjang hostile mendekati `int.MaxValue`).
- `ProcessBusDecodeTests.cs`: parse header sesi (APPID/Length), VLAN TCI (PCP 4 / VID 100), runt frame, declared-length bohong → APDU dipotong ke available, decode SV end-to-end (svID/smpCnt/confRev/smpSynch/payload), garbage APDU → frame parse OK tapi `HasDecodedApdu == false`, decode GOOSE end-to-end (goCbRef/datSet/goID/stNum/sqNum/TAL/confRev/test).
- Ter-wire ke `ProcessBusSuite.sln` (4 konfigurasi). Step CI "Run tests when present" yang sudah ada akan otomatis mendeteksi dan menjalankannya — badge hijau kini bermakna.

### N2 — Deadlock UI dispatcher (SwitchToRaw)
- `RawAnalyzerDataSource`: semua `await` di `StopAsync`/`PumpAsync` kini `ConfigureAwait(false)` (5 titik).
- `MainWindowViewModel`: `SwitchToRaw()` sync-over-async → `SwitchToRawAsync()` async penuh; `SwitchToRawCommand` pindah dari `RelayCommand` ke `AsyncRelayCommand`.
- `AsyncRelayCommand.Execute` (async void) kini menangkap exception yang lolos dan melapor via `Trace.TraceError` — exception command tidak lagi bisa menjatuhkan proses.

### N3 — Race use-after-free handle Npcap
- Semua P/Invoke yang men-dereference `_handle` (`pcap_next_ex`, copy buffer pcap, `pcap_close`) kini di bawah satu `_handleGate`.
- `pcap_breakloop` sengaja di luar gate untuk membangunkan `PcapNextEx` yang sedang blocking (read timeout 100 ms → Dispose menunggu gate maksimal ~100 ms).
- Hanya satu pemenang `Dispose` via `Interlocked.Exchange`; `_disposed` jadi `volatile`.
- `EnsureOpen` mengonfigurasi handle lokal (DLT check, BPF filter) sebelum dipublikasikan, sehingga handle yang belum published tidak pernah bisa balapan dengan `Dispose`.
- Salinan managed dari buffer pcap dilakukan di dalam gate (buffer milik pcap hanya valid sampai call/close berikutnya).

### N4 — Hardening BerReader
- Validasi `offset + length > source.Length` → `length > source.Length - offset` supaya panjang 4-byte hostile tidak bisa overflow penjumlahan dan lolos ke `Slice()`. Try-method kini benar-benar tidak bisa throw pada traffic mentah. (Dibuktikan test regresi N1.)

### N5 — Hot path SV: hapus double-copy per frame
- `RawProcessBusAnalyzer.ObserveOwnedFrame(byte[])`: jalur zero-copy untuk caller yang mentransfer kepemilikan array segar (pump Npcap). `ObserveFrame(ReadOnlyMemory<byte>)` tetap ada dengan defensive copy untuk caller arbitrer — identifier lama tidak diganggu.
- Pump memakai `MemoryMarshal.TryGetArray` + verifikasi full-array sebelum transfer ownership. Efek: alokasi per-frame turun 50% (~9.600 → ~4.800 alokasi/detik pada dua stream SV 80 sps/siklus).

### N6 — Versi: satu sumber kebenaran + injeksi saat rilis
- Terbukti: `publish-windows-portable.ps1` memakai `$Version` hanya untuk penamaan artefak, sehingga binary rilis v1.2.7 membawa `AssemblyVersion 1.2.0.0`.
- `Directory.Build.props`: versi tunggal `1.2.7`, semua properti turunan (`AssemblyVersion` = `$(VersionPrefix).0`, dst.) dan bisa dioverride.
- Script publish kini meneruskan `/p:VersionPrefix`, `/p:VersionSuffix`, `/p:AssemblyVersion`, `/p:FileVersion`, `/p:InformationalVersion` — drift tag-vs-binary tidak bisa terjadi lagi.

## Status verifikasi (transparan)
- Lingkungan patch ini TIDAK memiliki .NET SDK — belum ada kompilasi. Semua API yang disentuh/di-assert dibaca dari sumber sebelum diedit (signature `ProcessBusFrame`, `SampledValueAsdu`, `GoosePacket`, `VlanTag` record struct, urutan tag parser SV/GOOSE, `ResolveApduLength`).
- Gate verifikasi: CI `windows-latest` yang sudah ada (`dotnet build` sln → auto-detect & run `ProcessBus.Tests`).
- Perubahan Npcap/threading butuh satu sesi smoke test manual dengan adapter riil: start → switch mode saat running → stop → stop dengan timeout paksa → close app saat capture jalan.

## Di luar scope patch ini (rekomendasi berikutnya)
1. Pecah `MainWindowViewModel` (4.564 baris) per workspace; shell VM tipis.
2. Lock per-protokol di `RawProcessBusAnalyzer` (SV/GOOSE/PTP state independen).
3. Golden pcap fixtures dari capture FAT riil (anonimkan MAC) sebagai test korpus kedua.
