using ProcessBus.Core.Models.Scl;
using System.Globalization;
using System.Xml.Linq;

namespace ProcessBus.Core.Services.Scl;

public sealed class SclSemanticParser
{
    public SclProjectModel Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("SCL file path is empty.", nameof(filePath));

        var document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var root = document.Root ?? throw new InvalidDataException("SCL document has no root element.");
        if (!Is(root, "SCL"))
            throw new InvalidDataException("The selected file is not an IEC 61850 SCL document.");

        var warnings = new List<string>();
        var edition = DetectEdition(root, out var editionText);
        var header = root.Elements().FirstOrDefault(e => Is(e, "Header"));
        var ieds = ParseIeds(root).ToList();
        var typeIndex = SclTypeIndex.Build(root);
        var dataSets = ParseDataSets(root, typeIndex, warnings).ToList();
        var dataSetIndex = dataSets
            .GroupBy(d => d.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var addressIndex = ParseCommunicationAddresses(root);
        var gooseStreams = ParseGooseStreams(root, dataSetIndex, addressIndex, warnings).ToList();
        var svStreams = ParseSvStreams(root, dataSetIndex, addressIndex, warnings).ToList();

        if (dataSets.Count == 0)
            warnings.Add("No DataSet element was found. Semantic mapping will be limited.");
        if (gooseStreams.Count == 0)
            warnings.Add("No GSEControl stream was found.");
        if (svStreams.Count == 0)
            warnings.Add("No SampledValueControl stream was found.");

        return new SclProjectModel
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            NamespaceUri = root.Name.NamespaceName,
            HeaderId = Attr(header, "id"),
            HeaderVersion = Attr(header, "version"),
            HeaderRevision = Attr(header, "revision"),
            Edition = edition,
            EditionText = editionText,
            Ieds = ieds,
            DataSets = dataSets,
            GooseStreams = gooseStreams,
            SvStreams = svStreams,
            TypeSummaries = typeIndex.Summaries,
            Warnings = warnings
        };
    }

    private static IEnumerable<SclIedModel> ParseIeds(XElement root)
    {
        foreach (var ied in root.Descendants().Where(e => Is(e, "IED")))
        {
            yield return new SclIedModel
            {
                Name = Attr(ied, "name"),
                Manufacturer = Attr(ied, "manufacturer"),
                Type = Attr(ied, "type"),
                ConfigVersion = Attr(ied, "configVersion")
            };
        }
    }

    private static IEnumerable<SclDataSetModel> ParseDataSets(XElement root, SclTypeIndex typeIndex, List<string> warnings)
    {
        foreach (var ied in root.Elements().Where(e => Is(e, "IED")))
        {
            var iedName = Attr(ied, "name");
            foreach (var lDevice in ied.Descendants().Where(e => Is(e, "LDevice")))
            {
                var ldInst = Attr(lDevice, "inst");
                foreach (var ln in lDevice.Elements().Where(e => Is(e, "LN0") || Is(e, "LN")))
                {
                    var lnPath = BuildLnPath(ln);
                    foreach (var dataSet in ln.Elements().Where(e => Is(e, "DataSet")))
                    {
                        var dsName = Attr(dataSet, "name");
                        var entries = new List<SclDataSetEntryModel>();
                        var index = 1;
                        foreach (var fcda in dataSet.Elements().Where(e => Is(e, "FCDA")))
                        {
                            var entry = BuildDataSetEntry(iedName, fcda, index++, typeIndex, warnings);
                            entries.Add(entry);
                        }

                        yield return new SclDataSetModel
                        {
                            Key = DataSetKey(iedName, ldInst, dsName),
                            IedName = iedName,
                            LdInst = ldInst,
                            LnPath = lnPath,
                            Name = dsName,
                            Entries = entries
                        };
                    }
                }
            }
        }
    }

    private static SclDataSetEntryModel BuildDataSetEntry(string fallbackIedName, XElement fcda, int index, SclTypeIndex typeIndex, List<string> warnings)
    {
        var iedName = Attr(fcda, "iedName");
        if (string.IsNullOrWhiteSpace(iedName))
            iedName = fallbackIedName;

        var ldInst = Attr(fcda, "ldInst");
        var prefix = Attr(fcda, "prefix");
        var lnClass = Attr(fcda, "lnClass");
        var lnInst = Attr(fcda, "lnInst");
        var doName = Attr(fcda, "doName");
        var daName = Attr(fcda, "daName");
        var fc = Attr(fcda, "fc");
        var signalRef = BuildSignalReference(iedName, ldInst, prefix, lnClass, lnInst, doName, daName, fc);
        var typeInfo = typeIndex.Resolve(iedName, ldInst, prefix, lnClass, lnInst, doName, daName, fc);

        if (!typeInfo.Resolved && !string.IsNullOrWhiteSpace(signalRef))
            warnings.Add($"Type unresolved for {signalRef}. SCL may omit matching LN type or use a vendor-specific template pattern.");

        return new SclDataSetEntryModel
        {
            Index = index,
            SignalReference = signalRef,
            IedName = iedName,
            LdInst = ldInst,
            Prefix = prefix,
            LnClass = lnClass,
            LnInst = lnInst,
            DoName = doName,
            DaName = daName,
            Fc = string.IsNullOrWhiteSpace(fc) ? typeInfo.Fc : fc,
            Cdc = typeInfo.Cdc,
            BType = typeInfo.BType,
            TypeId = typeInfo.TypeId,
            EnumType = typeInfo.EnumType,
            IsQuality = string.Equals(daName, "q", StringComparison.OrdinalIgnoreCase) || daName.EndsWith(".q", StringComparison.OrdinalIgnoreCase),
            IsTimestamp = string.Equals(daName, "t", StringComparison.OrdinalIgnoreCase) || daName.EndsWith(".t", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static Dictionary<string, SclAddressModel> ParseCommunicationAddresses(XElement root)
    {
        var result = new Dictionary<string, SclAddressModel>(StringComparer.OrdinalIgnoreCase);
        var communication = root.Elements().FirstOrDefault(e => Is(e, "Communication"));
        if (communication is null)
            return result;

        foreach (var connectedAp in communication.Descendants().Where(e => Is(e, "ConnectedAP")))
        {
            var iedName = Attr(connectedAp, "iedName");
            foreach (var gse in connectedAp.Elements().Where(e => Is(e, "GSE")))
            {
                var key = StreamAddressKey("GOOSE", iedName, Attr(gse, "ldInst"), Attr(gse, "cbName"));
                result[key] = BuildAddress(gse, "GOOSE", iedName);
            }

            foreach (var smv in connectedAp.Elements().Where(e => Is(e, "SMV")))
            {
                var key = StreamAddressKey("SV", iedName, Attr(smv, "ldInst"), Attr(smv, "cbName"));
                result[key] = BuildAddress(smv, "SV", iedName);
            }
        }

        return result;
    }

    private static SclAddressModel BuildAddress(XElement streamAddressElement, string kind, string iedName)
    {
        var pValues = streamAddressElement.Descendants()
            .Where(e => Is(e, "P"))
            .GroupBy(p => Attr(p, "type"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (g.Last().Value ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);

        string Get(string type)
            => pValues.TryGetValue(type, out var value) ? value : string.Empty;

        return new SclAddressModel
        {
            Kind = kind,
            IedName = iedName,
            LdInst = Attr(streamAddressElement, "ldInst"),
            CbName = Attr(streamAddressElement, "cbName"),
            AppId = NormalizeAppId(Get("APPID")),
            DestinationMac = NormalizeMac(Get("MAC-Address")),
            VlanId = Get("VLAN-ID"),
            VlanPriority = Get("VLAN-PRIORITY")
        };
    }

    private static IEnumerable<SclGooseStreamModel> ParseGooseStreams(
        XElement root,
        IReadOnlyDictionary<string, SclDataSetModel> dataSets,
        IReadOnlyDictionary<string, SclAddressModel> addressIndex,
        List<string> warnings)
    {
        foreach (var ied in root.Elements().Where(e => Is(e, "IED")))
        {
            var iedName = Attr(ied, "name");
            foreach (var lDevice in ied.Descendants().Where(e => Is(e, "LDevice")))
            {
                var ldInst = Attr(lDevice, "inst");
                foreach (var ln0 in lDevice.Elements().Where(e => Is(e, "LN0")))
                {
                    foreach (var gseControl in ln0.Elements().Where(e => Is(e, "GSEControl")))
                    {
                        var name = Attr(gseControl, "name");
                        var datSet = Attr(gseControl, "datSet");
                        var ds = ResolveDataSet(dataSets, iedName, ldInst, datSet);
                        if (ds is null)
                            warnings.Add($"GOOSE {iedName}/{ldInst}/{name} references DataSet '{datSet}' but the DataSet was not found.");

                        addressIndex.TryGetValue(StreamAddressKey("GOOSE", iedName, ldInst, name), out var address);
                        yield return new SclGooseStreamModel
                        {
                            Kind = "GOOSE",
                            IedName = iedName,
                            LdInst = ldInst,
                            ControlName = name,
                            ControlBlockReference = $"{iedName}{ldInst}/LLN0$GO${name}",
                            GoId = Attr(gseControl, "appID"),
                            DataSetName = datSet,
                            DataSetReference = ds?.ShortReference ?? $"{iedName}/{ldInst}${datSet}",
                            ConfRev = IntAttr(gseControl, "confRev"),
                            AppId = address?.AppId ?? string.Empty,
                            DestinationMac = address?.DestinationMac ?? string.Empty,
                            VlanId = address?.VlanId ?? string.Empty,
                            VlanPriority = address?.VlanPriority ?? string.Empty,
                            Entries = ds?.Entries ?? Array.Empty<SclDataSetEntryModel>(),
                            MinTimeMs = IntAttr(gseControl, "minTime"),
                            MaxTimeMs = IntAttr(gseControl, "maxTime")
                        };
                    }
                }
            }
        }
    }

    private static IEnumerable<SclSvStreamModel> ParseSvStreams(
        XElement root,
        IReadOnlyDictionary<string, SclDataSetModel> dataSets,
        IReadOnlyDictionary<string, SclAddressModel> addressIndex,
        List<string> warnings)
    {
        foreach (var ied in root.Elements().Where(e => Is(e, "IED")))
        {
            var iedName = Attr(ied, "name");
            foreach (var lDevice in ied.Descendants().Where(e => Is(e, "LDevice")))
            {
                var ldInst = Attr(lDevice, "inst");
                foreach (var ln0 in lDevice.Elements().Where(e => Is(e, "LN0")))
                {
                    foreach (var svControl in ln0.Elements().Where(e => Is(e, "SampledValueControl")))
                    {
                        var name = Attr(svControl, "name");
                        var datSet = Attr(svControl, "datSet");
                        var ds = ResolveDataSet(dataSets, iedName, ldInst, datSet);
                        if (ds is null)
                            warnings.Add($"SV {iedName}/{ldInst}/{name} references DataSet '{datSet}' but the DataSet was not found.");

                        addressIndex.TryGetValue(StreamAddressKey("SV", iedName, ldInst, name), out var address);
                        var svId = Attr(svControl, "svID");
                        if (string.IsNullOrWhiteSpace(svId))
                            svId = Attr(svControl, "smvID");

                        yield return new SclSvStreamModel
                        {
                            Kind = "SV",
                            IedName = iedName,
                            LdInst = ldInst,
                            ControlName = name,
                            ControlBlockReference = $"{iedName}{ldInst}/LLN0$SV${name}",
                            SvId = svId,
                            SmvId = Attr(svControl, "smvID"),
                            DataSetName = datSet,
                            DataSetReference = ds?.ShortReference ?? $"{iedName}/{ldInst}${datSet}",
                            ConfRev = IntAttr(svControl, "confRev"),
                            AppId = address?.AppId ?? string.Empty,
                            DestinationMac = address?.DestinationMac ?? string.Empty,
                            VlanId = address?.VlanId ?? string.Empty,
                            VlanPriority = address?.VlanPriority ?? string.Empty,
                            Entries = ds?.Entries ?? Array.Empty<SclDataSetEntryModel>(),
                            SmpRate = IntAttr(svControl, "smpRate"),
                            SmpMod = Attr(svControl, "smpMod"),
                            NofAsdu = IntAttr(svControl, "nofASDU")
                        };
                    }
                }
            }
        }
    }

    private static SclDataSetModel? ResolveDataSet(IReadOnlyDictionary<string, SclDataSetModel> dataSets, string iedName, string ldInst, string datSet)
    {
        if (string.IsNullOrWhiteSpace(datSet))
            return null;

        if (dataSets.TryGetValue(DataSetKey(iedName, ldInst, datSet), out var direct))
            return direct;

        return dataSets.Values.FirstOrDefault(d =>
            string.Equals(d.IedName, iedName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(d.Name, datSet, StringComparison.OrdinalIgnoreCase));
    }

    private static SclEditionKind DetectEdition(XElement root, out string editionText)
    {
        var ns = root.Name.NamespaceName;
        var lower = ns.ToLowerInvariant();
        if (lower.Contains("2003"))
        {
            editionText = "IEC 61850-6 Edition 1 style namespace";
            return SclEditionKind.Edition1;
        }

        if (lower.Contains("2007b4") || lower.Contains("ed2.1") || lower.Contains("edition2.1"))
        {
            editionText = "IEC 61850-6 Edition 2.1 style namespace";
            return SclEditionKind.Edition21;
        }

        if (lower.Contains("2007") || lower.Contains("scl"))
        {
            editionText = "IEC 61850-6 Edition 2 / 2.x style namespace";
            return SclEditionKind.Edition2;
        }

        editionText = "SCL edition not identified from namespace";
        return SclEditionKind.Unknown;
    }

    private static string DataSetKey(string iedName, string ldInst, string dataSetName)
        => $"{iedName}|{ldInst}|{dataSetName}";

    private static string StreamAddressKey(string kind, string iedName, string ldInst, string cbName)
        => $"{kind}|{iedName}|{ldInst}|{cbName}";

    private static string BuildLnPath(XElement ln)
    {
        if (Is(ln, "LN0"))
            return "LLN0";

        return $"{Attr(ln, "prefix")}{Attr(ln, "lnClass")}{Attr(ln, "inst")}";
    }

    private static string BuildSignalReference(string iedName, string ldInst, string prefix, string lnClass, string lnInst, string doName, string daName, string fc)
    {
        var ln = $"{prefix}{lnClass}{lnInst}";
        var data = string.IsNullOrWhiteSpace(daName) ? doName : $"{doName}.{daName}";
        var fcPart = string.IsNullOrWhiteSpace(fc) ? string.Empty : $" [{fc}]";
        return $"{iedName}/{ldInst}/{ln}.{data}{fcPart}";
    }

    private static bool Is(XElement element, string localName)
        => string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal);

    private static string Attr(XElement? element, string localName)
    {
        if (element is null)
            return string.Empty;

        var attr = element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, localName, StringComparison.Ordinal));
        return attr?.Value?.Trim() ?? string.Empty;
    }

    private static int IntAttr(XElement element, string localName)
    {
        var text = Attr(element, localName);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static string NormalizeAppId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return trimmed.ToUpperInvariant();

        return $"0x{trimmed.ToUpperInvariant()}";
    }

    private static string NormalizeMac(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace('-', ':').ToUpperInvariant();

    private sealed class SclAddressModel
    {
        public string Kind { get; init; } = string.Empty;
        public string IedName { get; init; } = string.Empty;
        public string LdInst { get; init; } = string.Empty;
        public string CbName { get; init; } = string.Empty;
        public string AppId { get; init; } = string.Empty;
        public string DestinationMac { get; init; } = string.Empty;
        public string VlanId { get; init; } = string.Empty;
        public string VlanPriority { get; init; } = string.Empty;
    }

    private sealed class SclTypeIndex
    {
        private readonly Dictionary<string, XElement> _iedByName;
        private readonly Dictionary<string, XElement> _lNodeTypes;
        private readonly Dictionary<string, XElement> _doTypes;
        private readonly Dictionary<string, XElement> _daTypes;
        private readonly Dictionary<string, XElement> _enumTypes;

        public IReadOnlyList<SclTypeSummaryModel> Summaries { get; }

        private SclTypeIndex(
            Dictionary<string, XElement> iedByName,
            Dictionary<string, XElement> lNodeTypes,
            Dictionary<string, XElement> doTypes,
            Dictionary<string, XElement> daTypes,
            Dictionary<string, XElement> enumTypes,
            IReadOnlyList<SclTypeSummaryModel> summaries)
        {
            _iedByName = iedByName;
            _lNodeTypes = lNodeTypes;
            _doTypes = doTypes;
            _daTypes = daTypes;
            _enumTypes = enumTypes;
            Summaries = summaries;
        }

        public static SclTypeIndex Build(XElement root)
        {
            var ieds = root.Elements().Where(e => Is(e, "IED"))
                .Where(e => !string.IsNullOrWhiteSpace(Attr(e, "name")))
                .GroupBy(e => Attr(e, "name"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var lNodeTypes = root.Descendants().Where(e => Is(e, "LNodeType"))
                .Where(e => !string.IsNullOrWhiteSpace(Attr(e, "id")))
                .GroupBy(e => Attr(e, "id"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var doTypes = root.Descendants().Where(e => Is(e, "DOType"))
                .Where(e => !string.IsNullOrWhiteSpace(Attr(e, "id")))
                .GroupBy(e => Attr(e, "id"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var daTypes = root.Descendants().Where(e => Is(e, "DAType"))
                .Where(e => !string.IsNullOrWhiteSpace(Attr(e, "id")))
                .GroupBy(e => Attr(e, "id"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var enumTypes = root.Descendants().Where(e => Is(e, "EnumType"))
                .Where(e => !string.IsNullOrWhiteSpace(Attr(e, "id")))
                .GroupBy(e => Attr(e, "id"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var summaries = lNodeTypes.Select(e => new SclTypeSummaryModel { Kind = "LNodeType", Id = Attr(e.Value, "id"), ChildCount = e.Value.Elements().Count() })
                .Concat(doTypes.Select(e => new SclTypeSummaryModel { Kind = "DOType", Id = Attr(e.Value, "id"), Cdc = Attr(e.Value, "cdc"), ChildCount = e.Value.Elements().Count() }))
                .Concat(daTypes.Select(e => new SclTypeSummaryModel { Kind = "DAType", Id = Attr(e.Value, "id"), ChildCount = e.Value.Elements().Count() }))
                .Concat(enumTypes.Select(e => new SclTypeSummaryModel { Kind = "EnumType", Id = Attr(e.Value, "id"), ChildCount = e.Value.Elements().Count() }))
                .ToList();

            return new SclTypeIndex(ieds, lNodeTypes, doTypes, daTypes, enumTypes, summaries);
        }

        public TypeResolution Resolve(string iedName, string ldInst, string prefix, string lnClass, string lnInst, string doName, string daName, string fc)
        {
            var ln = FindLogicalNode(iedName, ldInst, prefix, lnClass, lnInst);
            if (ln is null)
                return TypeResolution.Unresolved(fc);

            var lnTypeId = Attr(ln, "lnType");
            if (!_lNodeTypes.TryGetValue(lnTypeId, out var lnType))
                return TypeResolution.Unresolved(fc);

            var doSegments = (doName ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (doSegments.Length == 0)
                return TypeResolution.Unresolved(fc);

            var dataObject = lnType.Elements().FirstOrDefault(e => Is(e, "DO") && SameName(e, doSegments[0]));
            if (dataObject is null)
                return TypeResolution.Unresolved(fc);

            var doTypeId = Attr(dataObject, "type");
            XElement? doType = _doTypes.TryGetValue(doTypeId, out var initialDoType) ? initialDoType : null;
            if (doType is null)
                return TypeResolution.Unresolved(fc);

            for (var i = 1; i < doSegments.Length; i++)
            {
                var sdo = doType.Elements().FirstOrDefault(e => Is(e, "SDO") && SameName(e, doSegments[i]));
                if (sdo is null || !_doTypes.TryGetValue(Attr(sdo, "type"), out var nextDoType))
                    return TypeResolution.Unresolved(fc, Attr(doType, "cdc"), doTypeId);
                doType = nextDoType;
                doTypeId = Attr(sdo, "type");
            }

            var cdc = Attr(doType, "cdc");
            var daSegments = (daName ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (daSegments.Length == 0)
                return new TypeResolution(true, fc, cdc, string.Empty, doTypeId, string.Empty);

            XElement? currentContainer = doType;
            string currentTypeId = doTypeId;
            string bType = string.Empty;
            string enumType = string.Empty;
            string resolvedFc = fc;

            for (var i = 0; i < daSegments.Length; i++)
            {
                var child = currentContainer?.Elements().FirstOrDefault(e => (Is(e, "DA") || Is(e, "BDA")) && SameName(e, daSegments[i]));
                if (child is null)
                    return new TypeResolution(false, resolvedFc, cdc, bType, currentTypeId, enumType);

                bType = Attr(child, "bType");
                if (string.IsNullOrWhiteSpace(resolvedFc))
                    resolvedFc = Attr(child, "fc");

                var childType = Attr(child, "type");
                if (string.Equals(bType, "Struct", StringComparison.OrdinalIgnoreCase) && _daTypes.TryGetValue(childType, out var daType))
                {
                    currentContainer = daType;
                    currentTypeId = childType;
                    continue;
                }

                if (string.Equals(bType, "Enum", StringComparison.OrdinalIgnoreCase) && _enumTypes.ContainsKey(childType))
                    enumType = childType;

                currentTypeId = childType;
            }

            return new TypeResolution(true, resolvedFc, cdc, bType, currentTypeId, enumType);
        }

        private XElement? FindLogicalNode(string iedName, string ldInst, string prefix, string lnClass, string lnInst)
        {
            if (!_iedByName.TryGetValue(iedName, out var ied))
                return null;

            var lDevice = ied.Descendants().FirstOrDefault(e => Is(e, "LDevice") && string.Equals(Attr(e, "inst"), ldInst, StringComparison.OrdinalIgnoreCase));
            if (lDevice is null)
                return null;

            if (string.Equals(lnClass, "LLN0", StringComparison.OrdinalIgnoreCase))
                return lDevice.Elements().FirstOrDefault(e => Is(e, "LN0"));

            return lDevice.Elements().FirstOrDefault(e => Is(e, "LN")
                && string.Equals(Attr(e, "prefix"), prefix, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Attr(e, "lnClass"), lnClass, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Attr(e, "inst"), lnInst, StringComparison.OrdinalIgnoreCase));
        }

        private static bool SameName(XElement element, string name)
            => string.Equals(Attr(element, "name"), name, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TypeResolution(bool Resolved, string Fc, string Cdc, string BType, string TypeId, string EnumType)
    {
        public static TypeResolution Unresolved(string fc, string cdc = "", string typeId = "")
            => new(false, fc, cdc, string.Empty, typeId, string.Empty);
    }
}
