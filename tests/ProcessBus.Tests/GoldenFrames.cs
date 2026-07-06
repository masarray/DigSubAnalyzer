using System.Buffers.Binary;
using System.Text;

namespace ProcessBus.Tests;

/// <summary>
/// Deterministic, byte-perfect IEC 61850 process-bus frame factory for tests.
/// TLV lengths are computed programmatically (short or long form) so test vectors
/// can never drift from their declared lengths.
/// </summary>
public static class GoldenFrames
{
    public const ushort SvEtherType = 0x88BA;
    public const ushort GooseEtherType = 0x88B8;

    public static byte[] Tlv(byte tag, params byte[][] parts)
    {
        var value = Concat(parts);
        byte[] lengthBytes;

        if (value.Length < 0x80)
        {
            lengthBytes = new[] { (byte)value.Length };
        }
        else if (value.Length <= 0xFF)
        {
            lengthBytes = new byte[] { 0x81, (byte)value.Length };
        }
        else
        {
            lengthBytes = new byte[] { 0x82, (byte)(value.Length >> 8), (byte)(value.Length & 0xFF) };
        }

        var result = new byte[1 + lengthBytes.Length + value.Length];
        result[0] = tag;
        lengthBytes.CopyTo(result, 1);
        value.CopyTo(result, 1 + lengthBytes.Length);
        return result;
    }

    public static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);

    public static byte[] U16(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] Concat(params byte[][] parts)
    {
        var total = parts.Sum(p => p.Length);
        var result = new byte[total];
        var offset = 0;

        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }

    /// <summary>
    /// Ethernet II frame: dst(6) src(6) [optional 802.1Q tag] etherType(2) payload.
    /// </summary>
    public static byte[] EthernetFrame(ushort etherType, byte[] payload, ushort? vlanTci = null)
    {
        var destination = new byte[] { 0x01, 0x0C, 0xCD, 0x04, 0x00, 0x01 };
        var source = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };

        return vlanTci is null
            ? Concat(destination, source, U16(etherType), payload)
            : Concat(destination, source, U16(0x8100), U16(vlanTci.Value), U16(etherType), payload);
    }

    /// <summary>
    /// IEC 61850-9-2 / 8-1 session header: APPID, Length (includes the 8 header bytes),
    /// Reserved1, Reserved2, then the APDU.
    /// </summary>
    public static byte[] ProcessBusPayload(ushort appId, byte[] apdu)
    {
        return Concat(
            U16(appId),
            U16((ushort)(8 + apdu.Length)),
            U16(0x0000),
            U16(0x0000),
            apdu);
    }

    /// <summary>
    /// Minimal, standard-shaped savPdu: [APPLICATION 0] { noASDU [0], seqOfAsdu [2] { SEQUENCE ASDU } }.
    /// </summary>
    public static byte[] SvApdu(
        string svId = "MU01",
        ushort smpCnt = 1,
        byte confRev = 1,
        byte smpSynch = 2,
        int samplePayloadLength = 8)
    {
        var samplePayload = new byte[samplePayloadLength];
        for (var i = 0; i < samplePayload.Length; i++)
            samplePayload[i] = (byte)i;

        var asdu = Tlv(0x30,
            Tlv(0x80, Ascii(svId)),                 // svID          [0]
            Tlv(0x82, U16(smpCnt)),                 // smpCnt        [2]
            Tlv(0x83, new[] { confRev }),           // confRev       [3]
            Tlv(0x85, new[] { smpSynch }),          // smpSynch      [5]
            Tlv(0x87, samplePayload));              // sample data   [7]

        return Tlv(0x60,                            // savPdu [APPLICATION 0], constructed
            Tlv(0x80, new byte[] { 0x01 }),         // noASDU        [0]
            Tlv(0xA2, asdu));                       // seqOfAsdu     [2], constructed
    }

    /// <summary>
    /// Minimal, standard-shaped goosePdu: [APPLICATION 1] with the usual field ordering.
    /// </summary>
    public static byte[] GooseApdu(
        string goCbRef = "IED1LD0/LLN0$GO$GoCb01",
        string dataSet = "IED1LD0/LLN0$DataSet01",
        string goId = "GO_TRIP",
        uint stNum = 3,
        uint sqNum = 7)
    {
        var utcTimestamp = new byte[8]; // raw 61850 UTC time, content not asserted

        return Tlv(0x61,                                        // goosePdu [APPLICATION 1], constructed
            Tlv(0x80, Ascii(goCbRef)),                          // gocbRef            [0]
            Tlv(0x81, new byte[] { 0x03, 0xE8 }),               // timeAllowedtoLive  [1] = 1000 ms
            Tlv(0x82, Ascii(dataSet)),                          // datSet             [2]
            Tlv(0x83, Ascii(goId)),                             // goID               [3]
            Tlv(0x84, utcTimestamp),                            // t                  [4]
            Tlv(0x85, new[] { (byte)stNum }),                   // stNum              [5]
            Tlv(0x86, new[] { (byte)sqNum }),                   // sqNum              [6]
            Tlv(0x87, new byte[] { 0x00 }),                     // test               [7]
            Tlv(0x88, new byte[] { 0x01 }),                     // confRev            [8]
            Tlv(0x89, new byte[] { 0x00 }),                     // ndsCom             [9]
            Tlv(0x8A, new byte[] { 0x01 }),                     // numDatSetEntries   [10]
            Tlv(0xAB, Tlv(0x83, new byte[] { 0x01 })));         // allData            [11] { boolean TRUE }
    }

    public static byte[] SvFrame(ushort appId = 0x4000, ushort? vlanTci = null)
    {
        return EthernetFrame(SvEtherType, ProcessBusPayload(appId, SvApdu()), vlanTci);
    }

    public static byte[] GooseFrame(ushort appId = 0x0001)
    {
        return EthernetFrame(GooseEtherType, ProcessBusPayload(appId, GooseApdu()));
    }
}
