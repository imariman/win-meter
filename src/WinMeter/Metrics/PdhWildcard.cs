using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WinMeter.Metrics;

internal sealed class PdhWildcard : IDisposable
{
    private readonly string _englishObject;
    private readonly string _englishCounter;
    private readonly List<IntPtr> _counters = new();
    private readonly List<string> _instances = new();
    private IntPtr _query;
    private DateTime _builtAt = DateTime.MinValue;
    private bool _primed;

    public PdhWildcard(string englishObject, string englishCounter)
    {
        _englishObject = englishObject;
        _englishCounter = englishCounter;
    }

    public void Collect()
    {
        if (_query == IntPtr.Zero)
        {
            if (DateTime.UtcNow - _builtAt < TimeSpan.FromSeconds(30))
            {
                return;
            }
        }
        else if (DateTime.UtcNow - _builtAt < TimeSpan.FromSeconds(30))
        {
            PdhCollectQueryData(_query);
            _primed = true;
            return;
        }

        Rebuild();
        if (_query != IntPtr.Zero)
        {
            PdhCollectQueryData(_query);
            _primed = true;
        }
    }

    public (string Instance, double Value)[] Read()
    {
        if (!_primed || _counters.Count == 0)
        {
            return Array.Empty<(string, double)>();
        }

        var result = new List<(string, double)>(_counters.Count);
        for (var i = 0; i < _counters.Count; i++)
        {
            result.Add((_instances[i], PdhNames.Value(_counters[i])));
        }

        return result.ToArray();
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

        var path = "\\" + PdhNames.Translate(_englishObject) + "(*)\\" + PdhNames.Translate(_englishCounter);
        foreach (var expanded in PdhNames.Expand(path))
        {
            if (PdhAddCounter(_query, expanded, IntPtr.Zero, out var counter) != 0)
            {
                continue;
            }

            _counters.Add(counter);
            _instances.Add(PdhNames.Instance(expanded));
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
        _instances.Clear();
        _primed = false;
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
