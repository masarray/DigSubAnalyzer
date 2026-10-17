using System.Buffers.Binary;

namespace ProcessBus.Iec61850.Raw.Sv;

/// <summary>
/// ARStack/9-2LE-style 4I+4V payload: eight (INT32 instMag, UINT32 q) pairs.
/// Do not apply this decoder to arbitrary SCL data layouts.
/// </summary>
public static class FourI4VQuality
{
    public enum MeasurementState { Good, Test, OperatorBlocked, Untrusted, UnknownLayout }

    public static MeasurementState Assess(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 64)
            return MeasurementState.UnknownLayout;

        bool test = false;
        bool blocked = false;
        for (var offset = 4; offset < payload.Length; offset += 8)
        {
            var q = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(offset, 4));
            // Validity and detail-quality bits (validity, overflow, outOfRange,
            // badReference, oscillatory, failure, oldData, inconsistent, inaccurate).
            if ((q & 0x03FFU) != 0)
                return MeasurementState.Untrusted;
            test |= (q & (1U << 11)) != 0;
            blocked |= (q & (1U << 12)) != 0;
        }

        if (blocked) return MeasurementState.OperatorBlocked;
        return test ? MeasurementState.Test : MeasurementState.Good;
    }
}
