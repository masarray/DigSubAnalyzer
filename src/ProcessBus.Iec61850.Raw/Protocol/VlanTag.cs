namespace ProcessBus.Iec61850.Raw.Protocol;

public readonly record struct VlanTag(byte PriorityCodePoint, bool DropEligible, ushort VlanId);
