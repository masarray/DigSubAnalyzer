using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProcessBus.Iec61850.Raw.Capture;

public sealed class NpcapRawFrameSource : IRawFrameSource
{
    private const int SnapshotLength = 65536;
    private const int ReadTimeoutMilliseconds = 100;
    private const int DataLinkEthernet = 1;
    private const int TimestampPrecisionMicroseconds = 0;
    private const int TimestampPrecisionNanoseconds = 1;
    private const string ProcessBusFilter = "ether proto 0x88ba or ether proto 0x88b8 or ether proto 0x88f7 or udp port 319 or udp port 320 or (vlan and (ether proto 0x88ba or ether proto 0x88b8 or ether proto 0x88f7 or udp port 319 or udp port 320))";

    private readonly string _deviceName;
    private readonly Action<string, string>? _diagnosticSink;
    private readonly object _handleGate = new();
    private IntPtr _handle;
    private int _timestampPrecision = TimestampPrecisionMicroseconds;
    private volatile bool _disposed;
    private int _disposeState;

    public NpcapRawFrameSource(string deviceName, Action<string, string>? diagnosticSink = null)
    {
        _deviceName = deviceName;
        _diagnosticSink = diagnosticSink;
    }

    public async IAsyncEnumerable<RawCapturedFrame> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureOpen();

        while (!cancellationToken.IsCancellationRequested && !_disposed)
        {
            var status = TryReadNextFrame(out var frame);

            if (status == ReadStatus.Frame && frame is not null)
            {
                yield return frame;
                continue;
            }

            if (status == ReadStatus.Stop)
                yield break;

            if (status == ReadStatus.Timeout)
                await Task.Yield();
        }
    }

    private enum ReadStatus
    {
        Frame,
        Skip,
        Timeout,
        Stop
    }

    private ReadStatus TryReadNextFrame(out RawCapturedFrame? frame)
    {
        frame = null;

        // Every wpcap call that dereferences _handle happens under _handleGate. Dispose
        // acquires the same gate before pcap_close, so a blocked/racing PcapNextEx can
        // never observe a freed handle (previously a native AV window on stop timeout).
        lock (_handleGate)
        {
            if (_disposed || _handle == IntPtr.Zero)
                return ReadStatus.Stop;

            var result = PcapNextEx(_handle, out var headerPtr, out var dataPtr);

            if (result == 0)
                return ReadStatus.Timeout;

            if (result < 0)
                return ReadStatus.Stop;

            if (headerPtr == IntPtr.Zero || dataPtr == IntPtr.Zero)
                return ReadStatus.Skip;

            var header = Marshal.PtrToStructure<PcapPacketHeader>(headerPtr);
            if (header.CapturedLength == 0 || header.CapturedLength > SnapshotLength)
            {
                _diagnosticSink?.Invoke("Warning", $"Npcap returned invalid captured length: {header.CapturedLength}");
                return ReadStatus.Skip;
            }

            // The pcap-owned buffers behind headerPtr/dataPtr are only valid until the
            // next pcap call or close, so the managed copy must complete inside the gate.
            var bytes = new byte[header.CapturedLength];
            Marshal.Copy(dataPtr, bytes, 0, checked((int)header.CapturedLength));

            frame = new RawCapturedFrame(
                bytes,
                ResolveCaptureTimeUtc(header.Timestamp, _timestampPrecision),
                ResolveCaptureTicks(header.Timestamp, _timestampPrecision));

            return ReadStatus.Frame;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        // Only one caller wins the close; others return immediately.
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        _disposed = true;

        // Break-loop is issued OUTSIDE _handleGate on purpose: the reader may be blocked
        // inside PcapNextEx while holding the gate, and pcap_breakloop is documented as
        // safe to call from another thread precisely to wake that blocked read.
        var handleForBreak = _handle;
        if (handleForBreak != IntPtr.Zero)
        {
            try
            {
                PcapBreakLoop(handleForBreak);
            }
            catch (EntryPointNotFoundException)
            {
                // Older WinPcap/Npcap builds may not export pcap_breakloop. The reader will
                // still exit on its next timeout tick, and close below waits for the gate.
            }
        }

        lock (_handleGate)
        {
            if (_handle != IntPtr.Zero)
            {
                PcapClose(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }

    private void EnsureOpen()
    {
        if (_handle != IntPtr.Zero)
            return;

        if (string.IsNullOrWhiteSpace(_deviceName) || _deviceName.StartsWith("index:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A real Npcap adapter device name is required for raw capture.");

        var handle = OpenActivatedCapture(_deviceName);

        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("Npcap open failed.");

        // Configure the still-private local handle first. It is not yet visible to
        // Dispose, so no gate is needed here; publication happens once, below.
        var linkType = PcapDataLink(handle);
        _diagnosticSink?.Invoke("Info", $"Npcap raw capture opened. DLT={linkType}, timestampPrecision={ResolveTimestampPrecisionText(_timestampPrecision)}, device={_deviceName}");
        if (linkType != DataLinkEthernet)
            _diagnosticSink?.Invoke("Warning", $"Selected adapter DLT={linkType}; raw SV/GOOSE/PTP decoding expects Ethernet DLT=1. Loopback adapters usually cannot decode process-bus Ethernet frames.");

        if (PcapCompile(handle, out var filter, ProcessBusFilter, 1, 0) == 0)
        {
            try
            {
                if (PcapSetFilter(handle, ref filter) == 0)
                    _diagnosticSink?.Invoke("Info", "Npcap process-bus/PTP BPF filter installed (SV, GOOSE, PTP L2, PTP UDP 319/320).");
                else
                    _diagnosticSink?.Invoke("Warning", $"Npcap setfilter failed; continuing unfiltered. {PcapGetErrorText(handle)}");
            }
            finally
            {
                PcapFreeCode(ref filter);
            }
        }
        else
        {
            _diagnosticSink?.Invoke("Warning", $"Npcap filter compile failed; continuing unfiltered. {PcapGetErrorText(handle)}");
        }

        lock (_handleGate)
        {
            if (_disposed)
            {
                PcapClose(handle);
                throw new ObjectDisposedException(nameof(NpcapRawFrameSource));
            }

            _handle = handle;
        }
    }

    private IntPtr OpenActivatedCapture(string deviceName)
    {
        try
        {
            return OpenWithCreateActivate(deviceName);
        }
        catch (EntryPointNotFoundException)
        {
            _diagnosticSink?.Invoke("Warning", "Npcap pcap_create API unavailable; falling back to pcap_open_live with microsecond timestamps.");
            return OpenWithOpenLive(deviceName);
        }
        catch (InvalidOperationException ex)
        {
            _diagnosticSink?.Invoke("Warning", $"{ex.Message}; falling back to pcap_open_live with microsecond timestamps.");
            return OpenWithOpenLive(deviceName);
        }
    }

    private IntPtr OpenWithCreateActivate(string deviceName)
    {
        var errbuf = new byte[256];
        var handle = PcapCreate(deviceName, errbuf);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"Npcap create failed: {ReadAnsiString(errbuf)}");

        try
        {
            ThrowIfPcapError(handle, PcapSetSnaplen(handle, SnapshotLength), "set snaplen");
            ThrowIfPcapError(handle, PcapSetPromisc(handle, 1), "set promiscuous mode");
            ThrowIfPcapError(handle, PcapSetTimeout(handle, ReadTimeoutMilliseconds), "set read timeout");
            TrySetImmediateMode(handle);

            TrySetNanosecondTimestampPrecision(handle);

            var activateResult = PcapActivate(handle);
            if (activateResult < 0)
                throw new InvalidOperationException($"Npcap activate failed: {PcapGetErrorText(handle)}");
            if (activateResult > 0)
                _diagnosticSink?.Invoke("Warning", $"Npcap activate warning {activateResult}: {PcapGetErrorText(handle)}");

            return handle;
        }
        catch
        {
            PcapClose(handle);
            throw;
        }
    }

    private void TrySetNanosecondTimestampPrecision(IntPtr handle)
    {
        try
        {
            var timestampResult = PcapSetTstampPrecision(handle, TimestampPrecisionNanoseconds);
            if (timestampResult == 0)
            {
                _timestampPrecision = TimestampPrecisionNanoseconds;
                return;
            }

            _timestampPrecision = TimestampPrecisionMicroseconds;
            _diagnosticSink?.Invoke("Warning", $"Npcap nanosecond timestamps unavailable; using microsecond precision. {PcapGetErrorText(handle)}");
        }
        catch (EntryPointNotFoundException)
        {
            _timestampPrecision = TimestampPrecisionMicroseconds;
            _diagnosticSink?.Invoke("Warning", "Npcap timestamp precision API unavailable; using microsecond timestamps.");
        }
    }

    private IntPtr OpenWithOpenLive(string deviceName)
    {
        var errbuf = new byte[256];
        var handle = PcapOpenLive(deviceName, SnapshotLength, 1, ReadTimeoutMilliseconds, errbuf);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"Npcap open failed: {ReadAnsiString(errbuf)}");

        _timestampPrecision = TimestampPrecisionMicroseconds;
        return handle;
    }

    private void TrySetImmediateMode(IntPtr handle)
    {
        try
        {
            var result = PcapSetImmediateMode(handle, 1);
            if (result != 0)
                _diagnosticSink?.Invoke("Warning", $"Npcap immediate mode unavailable; continuing with timeout buffering. {PcapGetErrorText(handle)}");
        }
        catch (EntryPointNotFoundException)
        {
            _diagnosticSink?.Invoke("Warning", "Npcap immediate mode API unavailable; continuing with timeout buffering.");
        }
    }

    private static void ThrowIfPcapError(IntPtr handle, int result, string operation)
    {
        if (result < 0)
            throw new InvalidOperationException($"Npcap {operation} failed: {PcapGetErrorText(handle)}");
    }

    private static string ResolveTimestampPrecisionText(int precision)
    {
        return precision == TimestampPrecisionNanoseconds ? "nanoseconds" : "microseconds";
    }

    private static DateTime ResolveCaptureTimeUtc(TimeVal timestamp, int precision)
    {
        var subsecondTicks = precision == TimestampPrecisionNanoseconds
            ? timestamp.Fraction / 100
            : timestamp.Fraction * 10;

        return DateTimeOffset
            .FromUnixTimeSeconds(timestamp.Seconds)
            .AddTicks(subsecondTicks)
            .UtcDateTime;
    }

    private static long ResolveCaptureTicks(TimeVal timestamp, int precision)
    {
        var subsecondNanoseconds = precision == TimestampPrecisionNanoseconds
            ? timestamp.Fraction
            : timestamp.Fraction * 1000L;

        return checked((timestamp.Seconds * System.Diagnostics.Stopwatch.Frequency) +
                       ((subsecondNanoseconds * System.Diagnostics.Stopwatch.Frequency) / 1_000_000_000L));
    }

    private static string ReadAnsiString(byte[] buffer)
    {
        var length = Array.IndexOf(buffer, (byte)0);
        if (length < 0)
            length = buffer.Length;

        return length == 0 ? string.Empty : System.Text.Encoding.ASCII.GetString(buffer, 0, length);
    }

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_open_live")]
    private static extern IntPtr PcapOpenLive(
        [MarshalAs(UnmanagedType.LPStr)] string device,
        int snaplen,
        int promiscuous,
        int timeoutMilliseconds,
        [Out] byte[] errbuf);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_create")]
    private static extern IntPtr PcapCreate(
        [MarshalAs(UnmanagedType.LPStr)] string device,
        [Out] byte[] errbuf);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_set_snaplen")]
    private static extern int PcapSetSnaplen(IntPtr handle, int snaplen);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_set_promisc")]
    private static extern int PcapSetPromisc(IntPtr handle, int promiscuous);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_set_timeout")]
    private static extern int PcapSetTimeout(IntPtr handle, int timeoutMilliseconds);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_set_immediate_mode")]
    private static extern int PcapSetImmediateMode(IntPtr handle, int immediateMode);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_set_tstamp_precision")]
    private static extern int PcapSetTstampPrecision(IntPtr handle, int timestampPrecision);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_activate")]
    private static extern int PcapActivate(IntPtr handle);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_next_ex")]
    private static extern int PcapNextEx(IntPtr handle, out IntPtr header, out IntPtr data);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_close")]
    private static extern void PcapClose(IntPtr handle);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_breakloop")]
    private static extern void PcapBreakLoop(IntPtr handle);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_datalink")]
    private static extern int PcapDataLink(IntPtr handle);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_geterr")]
    private static extern IntPtr PcapGetError(IntPtr handle);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_compile")]
    private static extern int PcapCompile(
        IntPtr handle,
        out BpfProgram program,
        [MarshalAs(UnmanagedType.LPStr)] string filter,
        int optimize,
        uint netmask);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_setfilter")]
    private static extern int PcapSetFilter(IntPtr handle, ref BpfProgram program);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_freecode")]
    private static extern void PcapFreeCode(ref BpfProgram program);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct BpfProgram
    {
        public readonly uint Length;
        public readonly IntPtr Instructions;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct TimeVal
    {
        public readonly int Seconds;
        public readonly int Fraction;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PcapPacketHeader
    {
        public readonly TimeVal Timestamp;
        public readonly uint CapturedLength;
        public readonly uint OriginalLength;
    }

    private static string PcapGetErrorText(IntPtr handle)
    {
        var error = PcapGetError(handle);
        return error == IntPtr.Zero
            ? string.Empty
            : Marshal.PtrToStringAnsi(error) ?? string.Empty;
    }
}
