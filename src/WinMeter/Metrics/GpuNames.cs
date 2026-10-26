using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WinMeter.Metrics;

internal readonly record struct GpuEngineLoad(string Luid, double Percent);

internal readonly record struct GpuIdentity(string Luid, string Name, long DedicatedBytes, long SharedBytes, bool Placeholder);

internal static class GpuNames
{
    private static GpuIdentity[] _cache = Array.Empty<GpuIdentity>();
    private static DateTime _readAt = DateTime.MinValue;

    public static GpuIdentity[] Read()
    {
        if (DateTime.UtcNow - _readAt < TimeSpan.FromSeconds(30))
        {
            return _cache;
        }

        _readAt = DateTime.UtcNow;
        _cache = Query();
        return _cache;
    }

    public static GpuAdapter[] Combine(GpuEngineLoad[] engines, GpuMemorySample[] memory, GpuIdentity[] names)
    {
        var map = new Dictionary<string, Bucket>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var bucket = Take(map, name.Luid);
            bucket.Name = name.Name;
            bucket.Dedicated = name.DedicatedBytes;
            bucket.Placeholder = name.Placeholder;
            bucket.Named = true;
        }

        foreach (var engine in engines)
        {
            Take(map, engine.Luid).Percent = engine.Percent;
        }

        foreach (var item in memory)
        {
            var bucket = Take(map, item.Luid);
            bucket.Used = item.DedicatedUsed;
            bucket.SharedUsed = item.SharedUsed;
        }

        var hardware = new List<GpuAdapter>();
        var fallback = new List<GpuAdapter>();
        foreach (var pair in map)
        {
            if (!pair.Key.StartsWith("luid_", StringComparison.Ordinal) && !pair.Value.Named)
            {
                continue;
            }

            if (!pair.Value.Named
                && pair.Value.Dedicated <= 0
                && pair.Value.Used <= 0
                && pair.Value.SharedUsed < 1024 * 1024
                && pair.Value.Percent < 1)
            {
                continue;
            }

            var name = pair.Value.Name.Length > 0 ? pair.Value.Name : "GPU";
            var adapter = new GpuAdapter(name, Math.Clamp(pair.Value.Percent, 0, 100), pair.Value.Used, pair.Value.Dedicated, pair.Value.SharedUsed);
            if (pair.Value.Placeholder)
            {
                fallback.Add(adapter);
            }
            else
            {
                hardware.Add(adapter);
            }
        }

        var chosen = hardware.Count > 0 ? hardware : fallback;
        chosen.Sort(static (left, right) =>
        {
            var byPercent = right.Percent.CompareTo(left.Percent);
            if (Math.Abs(left.Percent - right.Percent) > 0.5)
            {
                return byPercent;
            }

            var byCapacity = right.DedicatedBytes.CompareTo(left.DedicatedBytes);
            return byCapacity != 0 ? byCapacity : right.DedicatedUsedBytes.CompareTo(left.DedicatedUsedBytes);
        });
        return chosen.ToArray();
    }

    private static Bucket Take(Dictionary<string, Bucket> map, string luid)
    {
        if (luid.Length == 0)
        {
            luid = "gpu";
        }

        if (!map.TryGetValue(luid, out var bucket))
        {
            bucket = new Bucket();
            map[luid] = bucket;
        }

        return bucket;
    }

    private static GpuIdentity[] Query()
    {
        if (IntPtr.Size != 8)
        {
            return Array.Empty<GpuIdentity>();
        }

        try
        {
            var count = new EnumAdapters2();
            var status = D3DKMTEnumAdapters2(ref count);
            if (count.NumAdapters == 0 || count.NumAdapters > 64)
            {
                return Array.Empty<GpuIdentity>();
            }

            var stride = Marshal.SizeOf<AdapterInfo>();
            var buffer = Marshal.AllocHGlobal(stride * (int)count.NumAdapters);
            try
            {
                var list = new EnumAdapters2 { NumAdapters = count.NumAdapters, Adapters = buffer };
                status = D3DKMTEnumAdapters2(ref list);
                if (status != 0)
                {
                    return Array.Empty<GpuIdentity>();
                }

                var result = new List<GpuIdentity>((int)count.NumAdapters);
                for (var i = 0; i < count.NumAdapters; i++)
                {
                    var info = Marshal.PtrToStructure<AdapterInfo>(IntPtr.Add(buffer, i * stride));
                    var name = RegistryName(info.Handle);
                    var memory = Segment(info.Handle);
                    var close = new CloseAdapter { Handle = info.Handle };
                    D3DKMTCloseAdapter(ref close);
                    if (name.Length == 0 || IsPlaceholder(name))
                    {
                        continue;
                    }

                    var luid = "luid_0x" + unchecked((uint)info.LuidHigh).ToString("x8") + "_0x" + info.LuidLow.ToString("x8");
                    result.Add(new GpuIdentity(luid, name, memory.Dedicated, memory.Shared, false));
                }

                return result.ToArray();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            return Array.Empty<GpuIdentity>();
        }
    }

    private static string RegistryName(uint handle)
    {
        var bytes = Marshal.SizeOf<AdapterRegistryInfo>();
        var data = Marshal.AllocHGlobal(bytes);
        try
        {
            var query = new QueryAdapterInfo { Handle = handle, Type = 8, Data = data, Size = (uint)bytes };
            if (D3DKMTQueryAdapterInfo(ref query) != 0)
            {
                return "";
            }

            return (Marshal.PtrToStructure<AdapterRegistryInfo>(data).AdapterString ?? "").Trim();
        }
        catch (Exception)
        {
            return "";
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    private static (long Dedicated, long Shared) Segment(uint handle)
    {
        var bytes = Marshal.SizeOf<SegmentSize>();
        var data = Marshal.AllocHGlobal(bytes);
        try
        {
            var query = new QueryAdapterInfo { Handle = handle, Type = 3, Data = data, Size = (uint)bytes };
            if (D3DKMTQueryAdapterInfo(ref query) != 0)
            {
                return (0, 0);
            }

            var size = Marshal.PtrToStructure<SegmentSize>(data);
            var dedicated = size.DedicatedVideo > long.MaxValue ? 0 : (long)size.DedicatedVideo;
            var shared = size.Shared > long.MaxValue ? 0 : (long)size.Shared;
            return (dedicated, shared);
        }
        catch (Exception)
        {
            return (0, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    private static bool IsPlaceholder(string name)
    {
        return name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Basic Render", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Indirect", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Bucket
    {
        public string Name = "";
        public double Percent;
        public long Used;
        public long Dedicated;
        public long SharedUsed;
        public bool Placeholder;
        public bool Named;
    }

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTEnumAdapters2(ref EnumAdapters2 data);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTQueryAdapterInfo(ref QueryAdapterInfo data);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTCloseAdapter(ref CloseAdapter data);

    [StructLayout(LayoutKind.Sequential)]
    private struct EnumAdapters2
    {
        public uint NumAdapters;
        public IntPtr Adapters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterInfo
    {
        public uint Handle;
        public uint LuidLow;
        public int LuidHigh;
        public uint Sources;
        public int PresentMove;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct QueryAdapterInfo
    {
        [FieldOffset(0)] public uint Handle;
        [FieldOffset(4)] public uint Type;
        [FieldOffset(8)] public IntPtr Data;
        [FieldOffset(16)] public uint Size;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterRegistryInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string AdapterString;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string BiosString;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DacType;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ChipType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SegmentSize
    {
        public ulong DedicatedVideo;
        public ulong DedicatedSystem;
        public ulong Shared;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CloseAdapter
    {
        public uint Handle;
    }
}
