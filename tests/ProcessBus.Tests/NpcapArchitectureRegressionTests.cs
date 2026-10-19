using ProcessBus.Core.Services;
using Xunit;

namespace ProcessBus.Tests;

/// <summary>
/// The analyzer must not fabricate an Ethernet interface when wpcap cannot load.
/// Test deterministic native lookup policy without needing actual Npcap in CI.
/// </summary>
public sealed class NpcapArchitectureRegressionTests
{
    [Fact]
    public void TrustedNpcapDirectoryWinsOverLegacySystemDll()
    {
        var system = Path.Combine("C:", "Windows", "System32");
        var npcap = Path.Combine(system, "Npcap", "wpcap.dll");
        var old = Path.Combine(system, "wpcap.dll");
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { npcap, old };

        Assert.Equal(npcap,
            NpcapNativeLibrary.SelectLibraryPath(system, candidates.Contains));
    }

    [Fact]
    public void OnlySystemDirectoryCandidatesAreAccepted()
    {
        var system = Path.Combine("C:", "Windows", "System32");
        var unsafeLocal = Path.Combine("C:", "Tools", "ProcessBusInsight", "wpcap.dll");
        var candidate = NpcapNativeLibrary.SelectLibraryPath(system,
            path => string.Equals(path, unsafeLocal, StringComparison.OrdinalIgnoreCase));
        Assert.Null(candidate);

        var compatible = Path.Combine(system, "wpcap.dll");
        Assert.Equal(compatible, NpcapNativeLibrary.SelectLibraryPath(system,
            path => string.Equals(path, compatible, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MissingNativeLibraryNeverBecomesACapturableFakeEthernetAdapter()
    {
        var missing = new NetworkAdapterInfo
        {
            Id = "npcap-unavailable",
            Index = -1,
            Name = "Npcap unavailable — capture disabled",
            Description = "Native architecture mismatch",
            RawDeviceName = ""
        };
        Assert.False(missing.IsCaptureCapable);
        Assert.DoesNotContain("[0]", missing.ToString());
        Assert.False(new NetworkAdapterInfo { RawDeviceName = "index:0" }.IsCaptureCapable);
        Assert.True(new NetworkAdapterInfo
        {
            RawDeviceName = @"\\Device\\NPF_{12345678-1111-2222-3333-123456789ABC}"
        }.IsCaptureCapable);
    }
}
