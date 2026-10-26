using System;
using System.Runtime.InteropServices;

namespace WinMeter.Metrics;

internal sealed class CpuClock : IDisposable
{
    private const int ProcessorInformation = 11;
    private IntPtr _query;
    private IntPtr _counter;
    private double _powerMhz;

    public CpuClock()
    {
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0)
        {
            _query = IntPtr.Zero;
            return;
        }

        if (PdhAddEnglishCounter(_query, @"\Processor Information(_Total)\Actual Frequency", IntPtr.Zero, out _counter) != 0)
        {
            _counter = IntPtr.Zero;
        }
    }

    public double CurrentMhz { get; private set; }

    public void Collect()
    {
        ReadLimit();
        if (_query != IntPtr.Zero)
        {
            PdhCollectQueryData(_query);
        }

        var actual = PdhNames.Value(_counter);
        CurrentMhz = actual > 0 ? actual : _powerMhz;
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    private void ReadLimit()
    {
        var count = Environment.ProcessorCount;
        if (count <= 0 || count > 256)
        {
            return;
        }

        var stride = Marshal.SizeOf<ProcessorPowerInformation>();
        var buffer = Marshal.AllocHGlobal(stride * count);
        try
        {
            if (CallNtPowerInformation(ProcessorInformation, IntPtr.Zero, 0, buffer, stride * count) != 0)
            {
                return;
            }

            var current = 0u;
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<ProcessorPowerInformation>(IntPtr.Add(buffer, i * stride));
                if (info.CurrentMhz > current)
                {
                    current = info.CurrentMhz;
                }
            }

            if (current > 0)
            {
                _powerMhz = current;
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    [DllImport("powrprof.dll")]
    private static extern uint CallNtPowerInformation(int informationLevel, IntPtr inputBuffer, int inputBufferLength, IntPtr outputBuffer, int outputBufferLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }
}
