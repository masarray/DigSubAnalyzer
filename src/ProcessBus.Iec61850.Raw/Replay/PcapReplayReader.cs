using System.Buffers.Binary;

namespace ProcessBus.Iec61850.Raw.Replay;

public sealed class PcapReplayReader
{
    private const uint EthernetLinkType = 1;
    private readonly int _maximumFrameBytes;

    public PcapReplayReader(int maximumFrameBytes = 4 * 1024 * 1024)
    {
        if (maximumFrameBytes < 64)
            throw new ArgumentOutOfRangeException(nameof(maximumFrameBytes));

        _maximumFrameBytes = maximumFrameBytes;
    }

    public IEnumerable<PcapReplayFrame> Read(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("PCAP stream must be readable.", nameof(stream));

        var globalHeader = new byte[24];
        ReadExactly(stream, globalHeader, allowCleanEndOfStream: false);
        var format = ParseGlobalHeader(globalHeader);

        long sequence = 0;
        var recordHeader = new byte[16];

        while (ReadExactly(stream, recordHeader, allowCleanEndOfStream: true))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var seconds = ReadUInt32(recordHeader.AsSpan(0, 4), format.IsLittleEndian);
            var fraction = ReadUInt32(recordHeader.AsSpan(4, 4), format.IsLittleEndian);
            var includedLength = ReadUInt32(recordHeader.AsSpan(8, 4), format.IsLittleEndian);
            var originalLength = ReadUInt32(recordHeader.AsSpan(12, 4), format.IsLittleEndian);

            var fractionLimit = format.IsNanosecondResolution ? 1_000_000_000u : 1_000_000u;
            if (fraction >= fractionLimit)
                throw new InvalidDataException($"PCAP timestamp fraction is invalid at record {sequence + 1}.");
            if (includedLength == 0)
                throw new InvalidDataException($"PCAP record {sequence + 1} has an empty captured frame.");
            if (includedLength > format.SnapLength || includedLength > _maximumFrameBytes)
                throw new InvalidDataException($"PCAP record {sequence + 1} exceeds the configured frame boundary.");
            if (originalLength < includedLength)
                throw new InvalidDataException($"PCAP record {sequence + 1} declares original length smaller than captured length.");

            var frameBytes = new byte[checked((int)includedLength)];
            ReadExactly(stream, frameBytes, allowCleanEndOfStream: false);

            var timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            var ticks = format.IsNanosecondResolution
                ? fraction / 100u
                : fraction * 10u;
            timestamp = timestamp.AddTicks(ticks);

            sequence++;
            yield return new PcapReplayFrame(sequence, timestamp, frameBytes, originalLength);
        }
    }

    private static PcapFormat ParseGlobalHeader(ReadOnlySpan<byte> header)
    {
        var format = header[..4] switch
        {
            [0xD4, 0xC3, 0xB2, 0xA1] => new PcapFormat(true, false, 0),
            [0xA1, 0xB2, 0xC3, 0xD4] => new PcapFormat(false, false, 0),
            [0x4D, 0x3C, 0xB2, 0xA1] => new PcapFormat(true, true, 0),
            [0xA1, 0xB2, 0x3C, 0x4D] => new PcapFormat(false, true, 0),
            _ => throw new InvalidDataException("Unsupported PCAP magic. Only classic microsecond/nanosecond PCAP is accepted by this reader.")
        };

        var major = ReadUInt16(header.Slice(4, 2), format.IsLittleEndian);
        var minor = ReadUInt16(header.Slice(6, 2), format.IsLittleEndian);
        if (major != 2 || minor != 4)
            throw new InvalidDataException($"Unsupported PCAP version {major}.{minor}; expected 2.4.");

        var snapLength = ReadUInt32(header.Slice(16, 4), format.IsLittleEndian);
        if (snapLength == 0)
            throw new InvalidDataException("PCAP snap length must be greater than zero.");

        var linkType = ReadUInt32(header.Slice(20, 4), format.IsLittleEndian);
        if (linkType != EthernetLinkType)
            throw new InvalidDataException($"Unsupported PCAP link type {linkType}; Ethernet link type 1 is required.");

        return format with { SnapLength = snapLength };
    }

    private static bool ReadExactly(Stream stream, byte[] buffer, bool allowCleanEndOfStream)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read == 0)
            {
                if (allowCleanEndOfStream && offset == 0)
                    return false;

                throw new EndOfStreamException("PCAP ended in the middle of a header or frame.");
            }

            offset += read;
        }

        return true;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, bool littleEndian)
        => littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, bool littleEndian)
        => littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);

    private sealed record PcapFormat(bool IsLittleEndian, bool IsNanosecondResolution, uint SnapLength);
}

public sealed class PcapReplayFrame
{
    internal PcapReplayFrame(long sequenceNumber, DateTime captureTimeUtc, byte[] frameBytes, uint originalLength)
    {
        SequenceNumber = sequenceNumber;
        CaptureTimeUtc = captureTimeUtc;
        FrameBytes = frameBytes;
        OriginalLength = originalLength;
    }

    public long SequenceNumber { get; }
    public DateTime CaptureTimeUtc { get; }
    public ReadOnlyMemory<byte> FrameBytes { get; }
    public uint OriginalLength { get; }
}
