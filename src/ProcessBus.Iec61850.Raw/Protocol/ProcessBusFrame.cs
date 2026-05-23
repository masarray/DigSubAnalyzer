namespace ProcessBus.Iec61850.Raw.Protocol;

public sealed class ProcessBusFrame
{
    public ProcessBusFrame(
        EthernetFrame ethernet,
        ProcessBusFrameKind kind,
        ushort appId,
        ushort declaredLength,
        ushort reserved1,
        ushort reserved2,
        ReadOnlyMemory<byte> apdu,
        DateTime captureTimeUtc,
        long captureTicks,
        string transportText = "Ethernet",
        string networkSourceText = "N/A",
        string networkDestinationText = "N/A")
    {
        Ethernet = ethernet;
        Kind = kind;
        AppId = appId;
        DeclaredLength = declaredLength;
        Reserved1 = reserved1;
        Reserved2 = reserved2;
        Apdu = apdu;
        CaptureTimeUtc = captureTimeUtc;
        CaptureTicks = captureTicks;
        TransportText = transportText;
        NetworkSourceText = networkSourceText;
        NetworkDestinationText = networkDestinationText;
    }

    public EthernetFrame Ethernet { get; }
    public ProcessBusFrameKind Kind { get; }
    public ushort AppId { get; }
    public ushort DeclaredLength { get; }
    public ushort Reserved1 { get; }
    public ushort Reserved2 { get; }
    public ReadOnlyMemory<byte> Apdu { get; }
    public DateTime CaptureTimeUtc { get; }
    public long CaptureTicks { get; }
    public string TransportText { get; }
    public string NetworkSourceText { get; }
    public string NetworkDestinationText { get; }
}
