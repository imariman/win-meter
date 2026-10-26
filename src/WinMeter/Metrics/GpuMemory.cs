using System;
using System.Collections.Generic;

namespace WinMeter.Metrics;

internal readonly record struct GpuMemorySample(string Luid, long DedicatedUsed, long SharedUsed);

internal sealed class GpuMemory : IDisposable
{
    private readonly PdhWildcard _dedicated = new("GPU Adapter Memory", "Dedicated Usage");
    private readonly PdhWildcard _shared = new("GPU Adapter Memory", "Shared Usage");

    public void Collect()
    {
        _dedicated.Collect();
        _shared.Collect();
    }

    public GpuMemorySample[] Read()
    {
        try
        {
            var map = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            Add(_dedicated.Read(), map, 0);
            Add(_shared.Read(), map, 1);
            var result = new GpuMemorySample[map.Count];
            var index = 0;
            foreach (var pair in map)
            {
                result[index++] = new GpuMemorySample(pair.Key, pair.Value[0], pair.Value[1]);
            }

            return result;
        }
        catch (Exception)
        {
            return Array.Empty<GpuMemorySample>();
        }
    }

    public void Dispose()
    {
        _dedicated.Dispose();
        _shared.Dispose();
    }

    private static void Add((string Instance, double Value)[] samples, Dictionary<string, long[]> map, int slot)
    {
        foreach (var sample in samples)
        {
            var luid = PdhNames.Luid(sample.Instance);
            if (luid.Length == 0 || sample.Value <= 0 || sample.Value > long.MaxValue)
            {
                continue;
            }

            if (!map.TryGetValue(luid, out var used))
            {
                used = new long[2];
                map[luid] = used;
            }

            used[slot] += (long)sample.Value;
        }
    }
}
