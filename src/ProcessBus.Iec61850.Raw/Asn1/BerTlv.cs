namespace ProcessBus.Iec61850.Raw.Asn1;

public readonly record struct BerTlv(
    byte Tag,
    BerClass Class,
    bool Constructed,
    int TagNumber,
    ReadOnlyMemory<byte> Value);
