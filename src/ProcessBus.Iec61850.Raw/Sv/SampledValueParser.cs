using ProcessBus.Iec61850.Raw.Asn1;
using ProcessBus.Iec61850.Raw.Protocol;

namespace ProcessBus.Iec61850.Raw.Sv;

public static class SampledValueParser
{
    private const int SavPduApplicationTag = 0;

    public static bool TryParse(ProcessBusFrame frame, out SampledValuePacket packet)
    {
        packet = null!;

        if (frame.Kind != ProcessBusFrameKind.SampledValues)
            return false;

        var offset = 0;
        if (!BerReader.TryReadTlv(frame.Apdu, ref offset, out var savPdu) ||
            savPdu.Class != BerClass.Application ||
            savPdu.TagNumber != SavPduApplicationTag ||
            !savPdu.Constructed)
        {
            return false;
        }

        var asdus = new List<SampledValueAsdu>();

        foreach (var child in BerReader.ReadChildren(savPdu.Value))
        {
            if (child.Class == BerClass.ContextSpecific && child.TagNumber == 2)
                ReadAsduSequence(child.Value, asdus);
        }

        packet = new SampledValuePacket(frame, asdus);
        return true;
    }

    private static void ReadAsduSequence(ReadOnlyMemory<byte> sequenceValue, List<SampledValueAsdu> asdus)
    {
        foreach (var sequenceChild in BerReader.ReadChildren(sequenceValue))
        {
            if (sequenceChild.Tag == 0x30 && sequenceChild.Constructed)
                asdus.Add(ReadAsdu(sequenceChild.Value));
        }
    }

    private static SampledValueAsdu ReadAsdu(ReadOnlyMemory<byte> asduValue)
    {
        string? svId = null;
        string? dataSet = null;
        ushort? smpCnt = null;
        uint? confRev = null;
        ReadOnlyMemory<byte> refrTm = default;
        byte? smpSynch = null;
        ushort? smpRate = null;
        ushort? smpMod = null;
        ReadOnlyMemory<byte> samplePayload = default;

        foreach (var field in BerReader.ReadChildren(asduValue))
        {
            if (field.Class != BerClass.ContextSpecific)
                continue;

            switch (field.TagNumber)
            {
                case 0:
                    svId = BerReader.ReadString(field);
                    break;
                case 1:
                    dataSet = BerReader.ReadString(field);
                    break;
                case 2:
                    smpCnt = ToUInt16(BerReader.ReadUnsignedInteger(field));
                    break;
                case 3:
                    confRev = BerReader.ReadUnsignedInteger(field);
                    break;
                case 4:
                    refrTm = field.Value;
                    break;
                case 5:
                    smpSynch = ToByte(BerReader.ReadUnsignedInteger(field));
                    break;
                case 6:
                    smpRate = ToUInt16(BerReader.ReadUnsignedInteger(field));
                    break;
                case 7:
                    samplePayload = field.Value;
                    break;
                case 8:
                    smpMod = ToUInt16(BerReader.ReadUnsignedInteger(field));
                    break;
            }
        }

        return new SampledValueAsdu
        {
            SvId = svId,
            DataSet = dataSet,
            SmpCnt = smpCnt,
            ConfRev = confRev,
            RefrTm = refrTm,
            SmpSynch = smpSynch,
            SmpRate = smpRate,
            SmpMod = smpMod,
            SamplePayload = samplePayload
        };
    }

    private static ushort? ToUInt16(uint? value)
    {
        return value <= ushort.MaxValue ? (ushort)value.Value : null;
    }

    private static byte? ToByte(uint? value)
    {
        return value <= byte.MaxValue ? (byte)value.Value : null;
    }
}
