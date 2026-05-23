namespace ProcessBus.Core.Services;

public sealed class NetworkAdapterInfo
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; } = -1;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RawDeviceName { get; set; } = string.Empty;

    public override string ToString()
    {
        var label = string.IsNullOrWhiteSpace(Description) ? Name : $"{Name} ({Description})";
        return Index >= 0 ? $"[{Index}] {label}" : label;
    }
}
