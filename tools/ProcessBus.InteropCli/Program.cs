using System.Text.Json;
using System.Diagnostics;
using ProcessBus.Core.Models.Scl;
using ProcessBus.Core.Services.Scl;
using ProcessBus.Iec61850.Raw.Interop;

// Read-only offline acceptance screening. Never opens a NIC or operates an IED.
// Exit 0: screening executed and configured fields matched (not field acceptance).
// Exit 3: missing/conflicting/unresolved configured wire identity.
// Exit 2: invalid arguments, file errors, malformed/unsupported capture.
if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("""
        DigSubAnalyzer / Process Bus Insight — offline interop screening
        Usage: dotnet run --project tools/ProcessBus.InteropCli -- --pcap capture.pcap [--scl project.scd] [--json report.json] [--max-frames 15000000]
        Output is wire-observation screening only. NEVER proof of IED subscription,
        canonical VLAN visibility on another NIC, hardware timing or PTP lock.
        Supports classic Ethernet PCAP; data never leaves the local computer.
        """);
    return args.Length == 0 ? 2 : 0;
}

static string? Option(string[] arguments, string name)
{
    for (var i = 0; i < arguments.Length - 1; i++)
        if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
            return arguments[i + 1];
    return null;
}

var path = Option(args, "--pcap");
if (string.IsNullOrWhiteSpace(path))
{
    Console.Error.WriteLine("ERROR: --pcap is required.");
    return 2;
}

var frameLimit = 1_000_000;
if (Option(args, "--max-frames") is { } frameText &&
    !int.TryParse(frameText, out frameLimit))
{
    Console.Error.WriteLine("ERROR: --max-frames must be a positive integer.");
    return 2;
}

try
{
    IReadOnlyList<SclSvStreamModel> expected = Array.Empty<SclSvStreamModel>();
    if (Option(args, "--scl") is { } scl)
    {
        var model = new SclSemanticParser().Load(scl);
        expected = model.SvStreams;
        Console.WriteLine($"SCL expectations: {expected.Count} SV control blocks");
    }

    using var stream = File.OpenRead(path);
    // Local performance screening only: file-cache, CPU and disk affect these numbers.
    // Do not confuse offline replay throughput with live NIC drop-free performance.
    var process = Process.GetCurrentProcess();
    var initialCpu = process.TotalProcessorTime;
    var initialBytes = GC.GetTotalAllocatedBytes(precise: true);
    var watch = Stopwatch.StartNew();
    var report = new PcapInteropInspector().Inspect(stream, expected, frameLimit);
    watch.Stop();
    var replayCpu = process.TotalProcessorTime - initialCpu;
    var allocated = GC.GetTotalAllocatedBytes(precise: true) - initialBytes;
    var replayFps = report.FramesRead / Math.Max(0.001, watch.Elapsed.TotalSeconds);
    var allocatedPerFrame = allocated / Math.Max(1, report.FramesRead);

    Console.WriteLine($"Capture: {report.FramesRead} frames | {report.SvFrames} SV | " +
        $"{report.PtpMessages} PTP | {report.GooseFrames} GOOSE | " +
        $"{report.RejectedOrUnsupportedFrames} rejected/unsupported");
    Console.WriteLine($"Local replay screening: {replayFps:0} frames/s, " +
        $"CPU {replayCpu.TotalSeconds:0.##} s, " +
        $"allocated {allocatedPerFrame:0} bytes/frame (not live NIC benchmark)");
    Console.WriteLine($"SV identities: {report.SvStreams.Count} | " +
        $"capacity-rejected ASDUs: {report.CapacityRejectedAsdus}");
    foreach (var item in report.SvStreams)
    {
        var identity = item.IsArstackDiagnosticMirror ? "DIAGNOSTIC MIRROR" : "OBSERVED SV";
        Console.WriteLine($"  [{identity}] {item.SvId} APPID=0x{item.AppId:X4} " +
            $"MAC={item.DestinationMac} VLAN={(item.VlanId?.ToString() ?? "untagged")} " +
            $"PCP={(item.VlanPriority?.ToString() ?? "-")} " +
            $"ASDUs={item.AsduCount}, discontinuities={item.NonUnitCounterSteps}");
    }
    foreach (var item in report.Comparisons)
        Console.WriteLine($"  [{item.Status}] {item.ControlBlockReference}: {item.Explanation}");

    Console.WriteLine($"PTP: {report.PtpSources} source(s), " +
        $"matched two-step={report.MatchedTwoStep}, unmatched Follow_Up={report.UnmatchedFollowUp}");
    Console.WriteLine($"LIMITATION: {report.EvidenceLimitations}");

    if (Option(args, "--json") is { } json)
    {
        var jsonText = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(json, jsonText);
        Console.WriteLine("Local report saved. Inspect before sharing; may contain MAC/IED identities.");
    }

    if (report.FramesRead == 0 || report.RejectedOrUnsupportedFrames == report.FramesRead)
        return 2;

    return report.Comparisons.Any(x => x.Status != "WIRE_FIELDS_MATCH") ? 3 : 0;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or
                                UnauthorizedAccessException or ArgumentException or System.Xml.XmlException)
{
    Console.Error.WriteLine($"ERROR: {exception.Message}");
    return 2;
}
