using System.Reflection;
using System.Runtime.InteropServices;

namespace ProcessBus.Core.Services;

/// <summary>
/// Resolve Npcap from the native Windows system directory, never from the app
/// working directory, an arbitrary PATH entry, or a stale WinPcap DLL. The same
/// native module is shared between catalog enumeration and packet capture.
/// No driver installation occurs here; a missing/broken Npcap stays a clear error.
/// </summary>
public static class NpcapNativeLibrary
{
    private static readonly object RegistrationGate = new();
    private static readonly HashSet<Assembly> RegisteredAssemblies = new();
    private static readonly Lazy<IntPtr> PcapHandle = new(LoadPcap);

    /// <summary>
    /// Register once for each assembly that contains wpcap P/Invokes.
    /// Called from their static constructors, before the first native call.
    /// </summary>
    public static void RegisterFor(Assembly assembly)
    {
        if (!OperatingSystem.IsWindows())
            return;

        lock (RegistrationGate)
        {
            if (RegisteredAssemblies.Add(assembly))
                NativeLibrary.SetDllImportResolver(assembly, Resolve);
        }
    }

    // Also testable on non-Windows CI without loading a native Windows library.
    public static string? SelectLibraryPath(string systemDirectory, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(systemDirectory))
            return null;

        // Prefer the modern Npcap installation, including its matching Packet.dll.
        // Legacy system32 WinPcap-compatible deployments are a second choice only.
        var npcap = Path.Combine(systemDirectory, "Npcap", "wpcap.dll");
        if (fileExists(npcap))
            return npcap;

        var compatible = Path.Combine(systemDirectory, "wpcap.dll");
        return fileExists(compatible) ? compatible : null;
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "wpcap", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(libraryName, "wpcap.dll", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        return PcapHandle.Value;
    }

    private static IntPtr LoadPcap()
    {
        if (!Environment.Is64BitProcess)
            throw new BadImageFormatException(
                "Process Bus Insight requires the Windows x64 build for the supported Npcap capture path.");

        var system = Environment.SystemDirectory;
        var dll = SelectLibraryPath(system, File.Exists);
        if (dll is null)
            throw new DllNotFoundException(
                "Npcap wpcap.dll is not installed in the 64-bit Windows system directory. " +
                "Install or repair the official Npcap x64 driver, then restart Process Bus Insight.");

        // LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR + SYSTEM32 keeps Packet.dll/dependencies
        // away from unrelated PATH directories. Loading Packet.dll from the same
        // approved installation first prevents old x86 Packet.dll precedence.
        var options = DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.System32;
        var packet = Path.Combine(Path.GetDirectoryName(dll)!, "Packet.dll");
        if (File.Exists(packet))
            NativeLibrary.Load(packet, typeof(NpcapNativeLibrary).Assembly, options);

        try
        {
            return NativeLibrary.Load(dll, typeof(NpcapNativeLibrary).Assembly, options);
        }
        catch (BadImageFormatException ex)
        {
            throw new BadImageFormatException(
                $"Npcap native DLL architecture is incompatible with the x64 process ({dll}). " +
                "Repair/reinstall Npcap x64 and remove conflicting legacy WinPcap DLLs.", ex);
        }
        catch (DllNotFoundException ex)
        {
            throw new DllNotFoundException(
                $"Npcap library or a native dependency could not be loaded from {dll}. " +
                "Repair Npcap x64, then restart the analyzer.", ex);
        }
    }
}
