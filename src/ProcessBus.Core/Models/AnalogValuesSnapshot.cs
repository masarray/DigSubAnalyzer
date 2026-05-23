namespace ProcessBus.Core.Models;

public sealed class AnalogValuesSnapshot
{
    public ChannelValueModel Ia { get; init; } = new("Ia");
    public ChannelValueModel Ib { get; init; } = new("Ib");
    public ChannelValueModel Ic { get; init; } = new("Ic");
    public ChannelValueModel In { get; init; } = new("In");
    public ChannelValueModel Ua { get; init; } = new("Ua");
    public ChannelValueModel Ub { get; init; } = new("Ub");
    public ChannelValueModel Uc { get; init; } = new("Uc");
    public ChannelValueModel Un { get; init; } = new("Un");
}
