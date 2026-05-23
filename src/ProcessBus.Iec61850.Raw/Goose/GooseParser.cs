using ProcessBus.Iec61850.Raw.Asn1;
using ProcessBus.Iec61850.Raw.Protocol;

namespace ProcessBus.Iec61850.Raw.Goose;

public static class GooseParser
{
    private const int GoosePduApplicationTag = 1;

    public static bool TryParse(ProcessBusFrame frame, out GoosePacket packet)
    {
        packet = null!;

        if (frame.Kind != ProcessBusFrameKind.Goose)
            return false;

        var offset = 0;
        if (!BerReader.TryReadTlv(frame.Apdu, ref offset, out var goosePdu) ||
            goosePdu.Class != BerClass.Application ||
            goosePdu.TagNumber != GoosePduApplicationTag ||
            !goosePdu.Constructed)
        {
            return false;
        }

        string? goCbRef = null;
        uint? timeAllowedToLive = null;
        string? dataSet = null;
        string? goId = null;
        ReadOnlyMemory<byte> timestamp = default;
        uint? stNum = null;
        uint? sqNum = null;
        bool? test = null;
        uint? confRev = null;
        bool? needsCommission = null;
        uint? numDataSetEntries = null;
        ReadOnlyMemory<byte> allData = default;

        foreach (var field in BerReader.ReadChildren(goosePdu.Value))
        {
            if (field.Class != BerClass.ContextSpecific)
                continue;

            switch (field.TagNumber)
            {
                case 0:
                    goCbRef = BerReader.ReadString(field);
                    break;
                case 1:
                    timeAllowedToLive = BerReader.ReadUnsignedInteger(field);
                    break;
                case 2:
                    dataSet = BerReader.ReadString(field);
                    break;
                case 3:
                    goId = BerReader.ReadString(field);
                    break;
                case 4:
                    timestamp = field.Value;
                    break;
                case 5:
                    stNum = BerReader.ReadUnsignedInteger(field);
                    break;
                case 6:
                    sqNum = BerReader.ReadUnsignedInteger(field);
                    break;
                case 7:
                    test = BerReader.ReadBoolean(field);
                    break;
                case 8:
                    confRev = BerReader.ReadUnsignedInteger(field);
                    break;
                case 9:
                    needsCommission = BerReader.ReadBoolean(field);
                    break;
                case 10:
                    numDataSetEntries = BerReader.ReadUnsignedInteger(field);
                    break;
                case 11:
                    allData = field.Value;
                    break;
            }
        }

        var dataValues = GooseMmsDataDecoder.Decode(allData);

        packet = new GoosePacket(frame)
        {
            GoCbRef = goCbRef,
            TimeAllowedToLiveMilliseconds = timeAllowedToLive,
            DataSet = dataSet,
            GoId = goId,
            Timestamp = timestamp,
            StNum = stNum,
            SqNum = sqNum,
            Test = test,
            ConfRev = confRev,
            NeedsCommission = needsCommission,
            NumDataSetEntries = numDataSetEntries,
            AllData = allData,
            DataValues = dataValues
        };

        return true;
    }
}
