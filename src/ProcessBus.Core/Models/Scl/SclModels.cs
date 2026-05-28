namespace ProcessBus.Core.Models.Scl;

public enum SclEditionKind
{
    Unknown = 0,
    Edition1,
    Edition2,
    Edition21,
    Edition22
}

public sealed class SclProjectModel
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string NamespaceUri { get; init; } = string.Empty;
    public string HeaderId { get; init; } = string.Empty;
    public string HeaderVersion { get; init; } = string.Empty;
    public string HeaderRevision { get; init; } = string.Empty;
    public SclEditionKind Edition { get; init; }
    public string EditionText { get; init; } = "SCL edition unknown";
    public IReadOnlyList<SclIedModel> Ieds { get; init; } = Array.Empty<SclIedModel>();
    public IReadOnlyList<SclDataSetModel> DataSets { get; init; } = Array.Empty<SclDataSetModel>();
    public IReadOnlyList<SclGooseStreamModel> GooseStreams { get; init; } = Array.Empty<SclGooseStreamModel>();
    public IReadOnlyList<SclSvStreamModel> SvStreams { get; init; } = Array.Empty<SclSvStreamModel>();
    public IReadOnlyList<SclTypeSummaryModel> TypeSummaries { get; init; } = Array.Empty<SclTypeSummaryModel>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public string SummaryText =>
        $"{EditionText} - IED {Ieds.Count} - SV {SvStreams.Count} - GOOSE {GooseStreams.Count} - DataSet {DataSets.Count}";

    public static SclProjectModel Empty { get; } = new()
    {
        FileName = "No SCL loaded",
        EditionText = "No SCL loaded"
    };
}

public sealed class SclIedModel
{
    public string Name { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string ConfigVersion { get; init; } = string.Empty;
}

public sealed class SclDataSetModel
{
    public string Key { get; init; } = string.Empty;
    public string IedName { get; init; } = string.Empty;
    public string LdInst { get; init; } = string.Empty;
    public string LnPath { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<SclDataSetEntryModel> Entries { get; init; } = Array.Empty<SclDataSetEntryModel>();

    public string ShortReference => string.IsNullOrWhiteSpace(LnPath)
        ? $"{IedName}/{LdInst}${Name}"
        : $"{IedName}/{LdInst}/{LnPath}${Name}";
}

public sealed class SclDataSetEntryModel
{
    public int Index { get; init; }
    public string SignalReference { get; init; } = string.Empty;
    public string IedName { get; init; } = string.Empty;
    public string LdInst { get; init; } = string.Empty;
    public string Prefix { get; init; } = string.Empty;
    public string LnClass { get; init; } = string.Empty;
    public string LnInst { get; init; } = string.Empty;
    public string DoName { get; init; } = string.Empty;
    public string DaName { get; init; } = string.Empty;
    public string Fc { get; init; } = string.Empty;
    public string Cdc { get; init; } = string.Empty;
    public string BType { get; init; } = string.Empty;
    public string TypeId { get; init; } = string.Empty;
    public string EnumType { get; init; } = string.Empty;
    public bool IsQuality { get; init; }
    public bool IsTimestamp { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(SignalReference)
        ? $"Entry {Index}"
        : SignalReference;

    public string TypeText
    {
        get
        {
            var parts = new[] { Fc, Cdc, BType }.Where(x => !string.IsNullOrWhiteSpace(x));
            var text = string.Join(" - ", parts);
            return string.IsNullOrWhiteSpace(text) ? "type unresolved" : text;
        }
    }
}

public abstract class SclStreamModel
{
    public string Kind { get; init; } = string.Empty;
    public string IedName { get; init; } = string.Empty;
    public string LdInst { get; init; } = string.Empty;
    public string ControlName { get; init; } = string.Empty;
    public string ControlBlockReference { get; init; } = string.Empty;
    public string DataSetName { get; init; } = string.Empty;
    public string DataSetReference { get; init; } = string.Empty;
    public int ConfRev { get; init; }
    public string AppId { get; init; } = string.Empty;
    public string DestinationMac { get; init; } = string.Empty;
    public string VlanId { get; init; } = string.Empty;
    public string VlanPriority { get; init; } = string.Empty;
    public IReadOnlyList<SclDataSetEntryModel> Entries { get; init; } = Array.Empty<SclDataSetEntryModel>();

    public string TransportText
    {
        get
        {
            var app = string.IsNullOrWhiteSpace(AppId) ? "APPID N/A" : $"APPID {AppId}";
            var vlan = string.IsNullOrWhiteSpace(VlanId) ? "VLAN N/A" : $"VLAN {VlanId}";
            var prio = string.IsNullOrWhiteSpace(VlanPriority) ? "Prio N/A" : $"Prio {VlanPriority}";
            return $"{app} - {vlan} - {prio}";
        }
    }
}

public sealed class SclGooseStreamModel : SclStreamModel
{
    public string GoId { get; init; } = string.Empty;
    public int MinTimeMs { get; init; }
    public int MaxTimeMs { get; init; }
}

public sealed class SclSvStreamModel : SclStreamModel
{
    public string SvId { get; init; } = string.Empty;
    public string SmvId { get; init; } = string.Empty;
    public int SmpRate { get; init; }
    public string SmpMod { get; init; } = string.Empty;
    public int NofAsdu { get; init; }
}

public sealed class SclTypeSummaryModel
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Cdc { get; init; } = string.Empty;
    public int ChildCount { get; init; }
}
