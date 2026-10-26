using System;
using System.Collections.Generic;
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
        var adapters = ReadAdapters();
        var busiest = 0d;
        for (var i = 0; i < adapters.Length; i++)
        {
            if (adapters[i].Percent > busiest)
            {
                busiest = adapters[i].Percent;
            }
        }

        return busiest;
    }

    public GpuEngineLoad[] ReadAdapters()
    {
        if (!_primed || _counters.Count == 0)
        {
            return Array.Empty<GpuEngineLoad>();
        }

        var byEngine = new Dictionary<string, double>(StringComparer.Ordinal);
        for (var i = 0; i < _counters.Count; i++)
        {
            var value = PdhNames.Value(_counters[i]);
            if (value <= 0)
            {
                continue;
            }

            var luid = PdhNames.Luid(_paths[i]);
            var adapter = luid.Length > 0 ? luid : AdapterKey(_paths[i]);
            var key = adapter + "\n" + EngineType(_paths[i]);
            byEngine[key] = byEngine.TryGetValue(key, out var sum) ? sum + value : value;
        }

        var seen = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in byEngine)
        {
            var split = pair.Key.IndexOf('\n');
            var adapter = split < 0 ? pair.Key : pair.Key.Substring(0, split);
            var total = Math.Min(pair.Value, 100);
            seen[adapter] = seen.TryGetValue(adapter, out var current) ? Math.Max(current, total) : total;
        }

        var result = new GpuEngineLoad[seen.Count];
        var index = 0;
        foreach (var pair in seen)
        {
            result[index++] = new GpuEngineLoad(pair.Key, Math.Clamp(pair.Value, 0, 100));
        }

        return result;
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

        foreach (var path in PdhNames.Expand(CounterPath()))
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
        var engine = PdhNames.Translate("GPU Engine");
        var counter = PdhNames.Translate("Utilization Percentage");
        return "\\" + engine + "(*)\\" + counter;
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

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
