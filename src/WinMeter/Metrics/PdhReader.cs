using System;
using System.Runtime.InteropServices;

namespace WinMeter.Metrics;

/// <summary>
/// English PDH counters, so disk rates still work on a Turkish Windows install.
/// </summary>
internal sealed class PdhReader : IDisposable
{
    private const uint FormatDouble = 0x00000200;
    private IntPtr _query;
    private IntPtr _read;
    private IntPtr _write;
    private bool _primed;

    public bool Open()
    {
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0)
        {
            return false;
        }

        _read = Add(@"\PhysicalDisk(_Total)\Disk Read Bytes/sec")
            ?? Add(@"\LogicalDisk(_Total)\Disk Read Bytes/sec")
            ?? IntPtr.Zero;
        _write = Add(@"\PhysicalDisk(_Total)\Disk Write Bytes/sec")
            ?? Add(@"\LogicalDisk(_Total)\Disk Write Bytes/sec")
            ?? IntPtr.Zero;
        return _read != IntPtr.Zero || _write != IntPtr.Zero;
    }

    public void Collect()
    {
        if (_query == IntPtr.Zero)
        {
            return;
        }

        PdhCollectQueryData(_query);
        _primed = true;
    }

    public (double Read, double Write) Read()
    {
        if (!_primed)
        {
            return (0, 0);
        }

        return (Value(_read), Value(_write));
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    private IntPtr? Add(string path)
    {
        if (PdhAddEnglishCounter(_query, path, IntPtr.Zero, out var counter) != 0)
        {
            return null;
        }

        return counter;
    }

    private static double Value(IntPtr counter)
    {
        if (counter == IntPtr.Zero)
        {
            return 0;
        }

        var status = PdhGetFormattedCounterValue(counter, FormatDouble, out _, out var raw);
        if (status != 0 || double.IsNaN(raw.DoubleValue) || raw.DoubleValue < 0)
        {
            return 0;
        }

        return raw.DoubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(
        IntPtr counter,
        uint format,
        out uint type,
        out PdhFormattedValue value);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    [StructLayout(LayoutKind.Explicit)]
    private struct PdhFormattedValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double DoubleValue;
    }
}
