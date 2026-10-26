using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace WinMeter.Metrics;

internal readonly record struct WifiReading(int Signal, string Ssid)
{
    public static WifiReading None => new(-1, "");
}

internal sealed class WifiSignal : IDisposable
{
    private const int CurrentConnection = 7;
    private const int Connected = 1;
    private IntPtr _handle;
    private bool _missing;

    public WifiReading Read(NetworkInterface? adapter)
    {
        if (_missing || adapter == null || adapter.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
        {
            return WifiReading.None;
        }

        try
        {
            if (_handle == IntPtr.Zero && WlanOpenHandle(2, IntPtr.Zero, out _, out _handle) != 0)
            {
                _handle = IntPtr.Zero;
                return WifiReading.None;
            }

            if (WlanEnumInterfaces(_handle, IntPtr.Zero, out var list) != 0 || list == IntPtr.Zero)
            {
                return WifiReading.None;
            }

            try
            {
                var count = Marshal.ReadInt32(list);
                var stride = Marshal.SizeOf<WlanInterfaceInfo>();
                WifiReading? fallback = null;
                for (var i = 0; i < count && i < 8; i++)
                {
                    var info = Marshal.PtrToStructure<WlanInterfaceInfo>(IntPtr.Add(list, 8 + (i * stride)));
                    if (info.State != Connected)
                    {
                        continue;
                    }

                    var reading = Query(info.InterfaceGuid);
                    if (reading.Signal < 0)
                    {
                        continue;
                    }

                    var description = info.Description ?? "";
                    if (string.Equals(description, adapter.Description, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(description, adapter.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return reading;
                    }

                    fallback ??= reading;
                }

                return fallback ?? WifiReading.None;
            }
            finally
            {
                WlanFreeMemory(list);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _missing = true;
            return WifiReading.None;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return WifiReading.None;
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            WlanCloseHandle(_handle, IntPtr.Zero);
            _handle = IntPtr.Zero;
        }
    }

    private WifiReading Query(Guid id)
    {
        var guid = id;
        if (WlanQueryInterface(_handle, ref guid, CurrentConnection, IntPtr.Zero, out _, out var data, IntPtr.Zero) != 0
            || data == IntPtr.Zero)
        {
            return WifiReading.None;
        }

        try
        {
            var connection = Marshal.PtrToStructure<WlanConnectionAttributes>(data);
            var signal = connection.Association.SignalQuality;
            if (signal > 100)
            {
                return WifiReading.None;
            }

            return new WifiReading((int)signal, Ssid(connection.Association.Ssid));
        }
        finally
        {
            WlanFreeMemory(data);
        }
    }

    private static string Ssid(Dot11Ssid ssid)
    {
        if (ssid.Bytes == null || ssid.Length == 0 || ssid.Length > 32)
        {
            return "";
        }

        var text = Encoding.UTF8.GetString(ssid.Bytes, 0, (int)ssid.Length).Trim();
        foreach (var character in text)
        {
            if (character < ' ')
            {
                return "";
            }
        }

        return text;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiated, out IntPtr handle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(
        IntPtr handle,
        ref Guid interfaceGuid,
        int opcode,
        IntPtr reserved,
        out int dataSize,
        out IntPtr data,
        IntPtr opcodeValueType);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr handle, IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanInterfaceInfo
    {
        public Guid InterfaceGuid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Description;

        public int State;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Dot11Ssid
    {
        public uint Length;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Bytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanAssociation
    {
        public Dot11Ssid Ssid;
        public int BssType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] Bssid;

        public int PhyType;
        public uint PhyIndex;
        public uint SignalQuality;
        public uint RxRate;
        public uint TxRate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanConnectionAttributes
    {
        public int State;
        public int Mode;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Profile;

        public WlanAssociation Association;
    }
}
