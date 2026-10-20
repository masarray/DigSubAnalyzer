using System.Buffers.Binary;

namespace ProcessBus.Iec61850.Raw.Sv;

/// <summary>
/// ARStack/9-2LE-style 4I+4V payload: eight (INT32 instMag, UINT32 q) pairs.
/// Do not apply this decoder to arbitrary SCL data layouts.
/// </summary>
public static class FourI4VQuality
{
    public enum MeasurementState { Good, Test, OperatorBlocked, Untrusted, UnknownLayout }

    /// <summary>
    /// Read the q word for one explicitly known instMag/q pair. ElementIndex
    /// denotes the INT32 magnitude, not the byte offset or q element.
    /// Unknown layouts must never be assessed by an assumed pair ordering.
    /// </summary>
    public static MeasurementState AssessChannel(ReadOnlySpan<byte> payload, int elementIndex)
    {
        if (payload.Length != 64 || elementIndex < 0 ||
            elementIndex > 14 || (elementIndex & 1) != 0)
            return MeasurementState.UnknownLayout;

        var q = BinaryPrimitives.ReadUInt32BigEndian(
            payload.Slice((elementIndex + 1) * sizeof(int), sizeof(uint)));
        if ((q & ~((1U << 11) | (1U << 12))) != 0)
            return MeasurementState.Untrusted;
        if ((q & (1U << 12)) != 0)
            return MeasurementState.OperatorBlocked;
        return (q & (1U << 11)) != 0 ? MeasurementState.Test : MeasurementState.Good;
    }

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
            // Only explicitly modeled TEST/operatorBlocked flags may accompany
            // a trusted normal value. Unknown/reserved flags fail closed instead
            // of silently treating ambiguous quality bits as "good".
            if ((q & ~((1U << 11) | (1U << 12))) != 0)
                return MeasurementState.Untrusted;
            test |= (q & (1U << 11)) != 0;
            blocked |= (q & (1U << 12)) != 0;
        }

        if (blocked) return MeasurementState.OperatorBlocked;
        return test ? MeasurementState.Test : MeasurementState.Good;
    }
}
