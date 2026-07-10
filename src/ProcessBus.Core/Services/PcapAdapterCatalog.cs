using System.Runtime.InteropServices;

namespace ProcessBus.Core.Services;

public static class PcapAdapterCatalog
{
    public static List<NetworkAdapterInfo> GetAdapters()
    {
        try
        {
            return GetAdaptersCore();
        }
        catch (DllNotFoundException ex)
        {
            return BuildFallback($"Npcap/wpcap.dll not found: {ex.Message}");
        }
        catch (BadImageFormatException ex)
        {
            return BuildFallback($"Npcap architecture mismatch: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            return BuildFallback($"Npcap API entry point unavailable: {ex.Message}");
        }
        catch (Exception ex)
        {
            return BuildFallback($"Npcap adapter enumeration failed: {ex.Message}");
        }
    }

    private static List<NetworkAdapterInfo> GetAdaptersCore()
    {
        var errbuf = new byte[256];
        var result = FindAllDevices(out var devices, errbuf);

        if (result == -1 || devices == IntPtr.Zero)
        {
            var error = ReadAnsiString(errbuf);
            return BuildFallback(string.IsNullOrWhiteSpace(error)
                ? "Npcap enumerate returned no device list"
                : $"Npcap enumerate failed: {error}");
        }

        try
        {
            var adapters = new List<NetworkAdapterInfo>();
            var current = devices;
            var index = 0;

            while (current != IntPtr.Zero)
            {
                var device = Marshal.PtrToStructure<PcapIf>(current);
                var rawName = Marshal.PtrToStringAnsi(device.Name) ?? string.Empty;
                var description = Marshal.PtrToStringAnsi(device.Description) ?? string.Empty;

                adapters.Add(new NetworkAdapterInfo
                {
                    Id = index.ToString(),
                    Index = index,
                    Name = BuildDisplayName(rawName, description, index),
                    Description = description,
                    RawDeviceName = rawName
                });

                current = device.Next;
                index++;
            }

            return adapters.Count > 0
                ? adapters
                : BuildFallback("Npcap returned no devices");
        }
        finally
        {
            PcapFreeAllDevs(devices);
        }
    }

    private static List<NetworkAdapterInfo> BuildFallback(string reason)
    {
        return new List<NetworkAdapterInfo>
        {
            new()
            {
                Id = "0",
                Index = 0,
                Name = "Ethernet",
                Description = $"Fallback adapter entry ({reason})",
                RawDeviceName = "index:0"
            }
        };
    }

    private static int FindAllDevices(out IntPtr devices, byte[] errbuf)
    {
        try
        {
            return PcapFindAllDevs(out devices, errbuf);
        }
        catch (EntryPointNotFoundException)
        {
            return PcapFindAllDevsEx("rpcap://", IntPtr.Zero, out devices, errbuf);
        }
    }

    private static string BuildDisplayName(string rawName, string description, int index)
    {
        if (!string.IsNullOrWhiteSpace(description))
            return description;

        if (!string.IsNullOrWhiteSpace(rawName))
            return rawName;

        return $"Adapter {index}";
    }

    private static string ReadAnsiString(byte[] buffer)
    {
        var length = Array.IndexOf(buffer, (byte)0);
        if (length < 0)
            length = buffer.Length;

        return length == 0 ? string.Empty : System.Text.Encoding.ASCII.GetString(buffer, 0, length);
    }

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_findalldevs_ex")]
    private static extern int PcapFindAllDevsEx(
        [MarshalAs(UnmanagedType.LPStr)] string source,
        IntPtr auth,
        out IntPtr alldevs,
        [Out] byte[] errbuf);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_findalldevs")]
    private static extern int PcapFindAllDevs(
        out IntPtr alldevs,
        [Out] byte[] errbuf);

    [DllImport("wpcap", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pcap_freealldevs")]
    private static extern void PcapFreeAllDevs(IntPtr alldevs);

    [StructLayout(LayoutKind.Sequential)]
    private struct PcapIf
    {
        public IntPtr Next;
        public IntPtr Name;
        public IntPtr Description;
        public IntPtr Addresses;
        public uint Flags;
    }
}
