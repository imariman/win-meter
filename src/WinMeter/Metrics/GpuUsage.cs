using System;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace WinMeter.Metrics;

/// <summary>
/// GPU 3D/compute/copy/video load from English PDH names, translated for the installed Windows language.
/// </summary>
internal sealed class GpuUsage : IDisposable
{
    private static readonly string[] EngineTypes = { "3D", "Compute", "Copy", "VideoDecode", "VideoEncode", "VideoProcessing" };
    private IntPtr _query;
    private readonly List<IntPtr> _counters = new();
    private readonly List<string> _paths = new();
    private DateTime _builtAt = DateTime.MinValue;
    private bool _primed;

    public bool HasEngines => _counters.Count > 0;

    public void Collect()
    {
        if (_query == IntPtr.Zero || DateTime.UtcNow - _builtAt > TimeSpan.FromSeconds(10))
        {
            Rebuild();
        }

        if (_query == IntPtr.Zero)
        {
            return;
        }

        PdhCollectQueryData(_query);
        _primed = true;
    }

    public double Read()
    {
        if (!_primed || _counters.Count == 0)
        {
            return 0;
        }

        var byAdapter = new Dictionary<string, double>(StringComparer.Ordinal);
        for (var i = 0; i < _counters.Count; i++)
        {
            var value = Formatted(_counters[i]);
            if (value <= 0)
            {
                continue;
            }

            var adapter = AdapterKey(_paths[i]);
            var type = EngineType(_paths[i]);
            var key = adapter + "\n" + type;
            byAdapter[key] = byAdapter.TryGetValue(key, out var sum) ? sum + value : value;
        }

        var busiest = 0d;
        var seen = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var pair in byAdapter)
        {
            var split = pair.Key.IndexOf('\n');
            var adapter = split < 0 ? pair.Key : pair.Key.Substring(0, split);
            var total = Math.Min(pair.Value, 100);
            seen[adapter] = seen.TryGetValue(adapter, out var current) ? Math.Max(current, total) : total;
        }

        foreach (var total in seen.Values)
        {
            if (total > busiest)
            {
                busiest = total;
            }
        }

        return Math.Clamp(busiest, 0, 100);
    }

    public void Dispose()
    {
        Close();
    }

    private void Rebuild()
    {
        Close();
        _builtAt = DateTime.UtcNow;
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0)
        {
            _query = IntPtr.Zero;
            return;
        }

        foreach (var path in Expand(CounterPath()))
        {
            if (!IsUsefulEngine(path) || PdhAddCounter(_query, path, IntPtr.Zero, out var counter) != 0)
            {
                continue;
            }

            _counters.Add(counter);
            _paths.Add(path);
        }

        if (_counters.Count == 0)
        {
            Close();
        }

        _primed = false;
    }

    private void Close()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }

        _counters.Clear();
        _paths.Clear();
        _primed = false;
    }

    private static string CounterPath()
    {
        var engine = Translate("GPU Engine");
        var counter = Translate("Utilization Percentage");
        return "\\" + engine + "(*)\\" + counter;
    }

    private static string[] Expand(string path)
    {
        var chars = 2048;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var buffer = Marshal.AllocHGlobal(chars * 2);
            try
            {
                var size = (uint)chars;
                var status = PdhExpandWildCardPath(null, path, buffer, ref size, 0);
                if (status == 0x800007D2)
                {
                    chars = (int)size + 16;
                    continue;
                }

                if (status != 0 || size < 2)
                {
                    return Array.Empty<string>();
                }

                var copy = (int)Math.Min(size, (uint)chars);
                var text = new char[copy];
                Marshal.Copy(buffer, text, 0, copy);
                return new string(text).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return Array.Empty<string>();
    }

    private static bool IsUsefulEngine(string path)
    {
        var type = EngineType(path);
        for (var i = 0; i < EngineTypes.Length; i++)
        {
            if (string.Equals(type, EngineTypes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string EngineType(string path)
    {
        const string marker = "engtype_";
        var index = path.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return "";
        }

        var start = index + marker.Length;
        var end = start;
        while (end < path.Length && char.IsLetterOrDigit(path[end]))
        {
            end++;
        }

        return path.Substring(start, end - start);
    }

    private static string AdapterKey(string path)
    {
        const string marker = "_phys_";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return "0";
        }

        var start = index + marker.Length;
        var end = start;
        while (end < path.Length && char.IsDigit(path[end]))
        {
            end++;
        }

        return path.Substring(start, end - start);
    }

    private static double Formatted(IntPtr counter)
    {
        if (PdhGetFormattedCounterValue(counter, 0x00000200, out _, out var raw) != 0
            || double.IsNaN(raw.DoubleValue)
            || raw.DoubleValue < 0)
        {
            return 0;
        }

        return raw.DoubleValue;
    }

    private static string Translate(string english)
    {
        try
        {
            using var perflib = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Perflib");
            if (perflib == null)
            {
                return english;
            }

            var language = perflib.GetValue("CurrentLanguage") as string ?? "009";
            using var englishKey = perflib.OpenSubKey("009");
            using var localKey = perflib.OpenSubKey(language);
            var englishMap = Names(englishKey);
            var localMap = Names(localKey);
            foreach (var pair in englishMap)
            {
                if (!string.Equals(pair.Value, english, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return localMap.TryGetValue(pair.Key, out var local) && local.Length > 0 ? local : english;
            }
        }
        catch (Exception)
        {
        }

        return english;
    }

    private static Dictionary<int, string> Names(RegistryKey? key)
    {
        var map = new Dictionary<int, string>();
        if (key?.GetValue("Counter") is not string[] rows)
        {
            return map;
        }

        for (var i = 0; i + 1 < rows.Length; i += 2)
        {
            if (int.TryParse(rows[i], out var index))
            {
                map[index] = rows[i + 1];
            }
        }

        return map;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhExpandWildCardPath(string? dataSource, string wildCardPath, IntPtr expanded, ref uint pathListLength, uint flags);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhFormattedValue value);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    [StructLayout(LayoutKind.Explicit)]
    private struct PdhFormattedValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double DoubleValue;
    }
}
