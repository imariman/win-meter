using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WinMeter.Metrics;

internal sealed class SystemSampler : IDisposable
{
    private const int HistoryLimit = 3600;
    private const int ProcessorPerformanceClass = 8;
    private const uint StatusInfoLengthMismatch = 0xC0000004;
    private readonly object _gate = new();
    private readonly List<Snapshot> _history = new();
    private readonly PdhReader _disk = new();
    private readonly GpuUsage _gpu = new();
    private readonly CancellationTokenSource _cancel = new();
    private Task? _loop;
    private NetworkInterface? _adapter;
    private DateTime _adapterCheckedAt;
    private Snapshot? _latest;

    public event EventHandler<Snapshot>? Updated;

    public Snapshot? Latest
    {
        get
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }

    public Snapshot[] CopyHistory()
    {
        lock (_gate)
        {
            return _history.ToArray();
        }
    }

    public void Start()
    {
        if (_loop != null)
        {
            return;
        }

        if (_disk.Open())
        {
            _disk.Collect();
        }

        try
        {
            _gpu.Collect();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        _loop = Task.Run(LoopAsync);
    }

    public void Dispose()
    {
        _cancel.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _disk.Dispose();
        _gpu.Dispose();
        _cancel.Dispose();
    }

    private async Task LoopAsync()
    {
        CpuTimes? previousCpu = ReadCpu();
        NetPoint? previousNet = ReadNet();
        CoreTimes[]? previousCores = ReadCoreTimes();
        var previousAt = DateTime.UtcNow;

        while (!_cancel.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _cancel.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var now = DateTime.UtcNow;
            var elapsed = (now - previousAt).TotalSeconds;
            previousAt = now;
            var cpu = ReadCpu();
            var net = ReadNet();
            var coreTimes = ReadCoreTimes();

            if (elapsed <= 0 || elapsed > 5 || previousCpu == null || cpu == null || previousNet == null || net == null)
            {
                previousCpu = cpu;
                previousNet = net;
                previousCores = coreTimes;
                TryCollectDisk();
                TryCollectGpu();
                continue;
            }

            double cpuPercent = 0;
            if (cpu.Value.Total > previousCpu.Value.Total && cpu.Value.Busy >= previousCpu.Value.Busy)
            {
                var total = cpu.Value.Total - previousCpu.Value.Total;
                var busy = cpu.Value.Busy - previousCpu.Value.Busy;
                cpuPercent = Math.Clamp(busy * 100.0 / total, 0, 100);
            }

            var down = Rate(previousNet.Value.Received, net.Value.Received, elapsed);
            var up = Rate(previousNet.Value.Sent, net.Value.Sent, elapsed);
            if (!string.Equals(previousNet.Value.Id, net.Value.Id, StringComparison.Ordinal))
            {
                down = 0;
                up = 0;
            }

            var diskIo = ReadDiskIo();
            var memory = ReadMemory();
            var disk = ReadDisk();
            var snapshot = new Snapshot(
                cpuPercent,
                memory.Percent,
                memory.Used,
                memory.Total,
                Environment.ProcessorCount,
                disk.UsedPercent,
                disk.Name,
                disk.FreeBytes,
                diskIo.Read,
                diskIo.Write,
                down,
                up,
                net.Value.Name,
                HasDelta: true,
                PercentCores(previousCores, coreTimes),
                memory.CommitUsed,
                memory.CommitLimit,
                disk.TotalBytes,
                _gpu.HasEngines,
                ReadGpu());

            previousCpu = cpu;
            previousNet = net;
            previousCores = coreTimes;
            Publish(snapshot);
        }
    }

    private void Publish(Snapshot snapshot)
    {
        lock (_gate)
        {
            _latest = snapshot;
            _history.Add(snapshot);
            if (_history.Count > HistoryLimit)
            {
                _history.RemoveAt(0);
            }
        }

        Updated?.Invoke(this, snapshot);
    }

    private void TryCollectDisk()
    {
        try
        {
            _disk.Collect();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void TryCollectGpu()
    {
        try
        {
            _gpu.Collect();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private double ReadGpu()
    {
        try
        {
            _gpu.Collect();
            return _gpu.Read();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return 0;
        }
    }

    private (double Read, double Write) ReadDiskIo()
    {
        try
        {
            _disk.Collect();
            return _disk.Read();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return (0, 0);
        }
    }

    private static CpuTimes? ReadCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return null;
        }

        return new CpuTimes(ToUInt64(idle), ToUInt64(kernel), ToUInt64(user));
    }

    private static RamPoint ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
        {
            return new RamPoint(0, 0, 0, 0, 0);
        }

        var used = (long)(status.TotalPhysical - status.AvailablePhysical);
        var commitUsed = status.TotalPageFile > status.AvailablePageFile
            ? (long)(status.TotalPageFile - status.AvailablePageFile)
            : 0;
        return new RamPoint(status.MemoryLoad, used, (long)status.TotalPhysical, commitUsed, (long)status.TotalPageFile);
    }

    private static DiskPoint ReadDisk()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return new DiskPoint(root.TrimEnd('\\'), 0, 0, 0);
            }

            var free = drive.AvailableFreeSpace;
            var usedPercent = Math.Clamp((1 - (free / (double)drive.TotalSize)) * 100, 0, 100);
            var name = drive.Name.TrimEnd('\\');
            if (!string.IsNullOrWhiteSpace(drive.VolumeLabel))
            {
                name += " " + drive.VolumeLabel;
            }

            return new DiskPoint(name, usedPercent, free, drive.TotalSize);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new DiskPoint("Disk", 0, 0, 0);
        }
    }

    private NetPoint? ReadNet()
    {
        try
        {
            var adapter = SelectAdapter();
            if (adapter == null)
            {
                return new NetPoint("", "not connected", 0, 0);
            }

            var stats = adapter.GetIPv4Statistics();
            var name = string.IsNullOrWhiteSpace(adapter.Name) ? adapter.Description : adapter.Name;
            return new NetPoint(adapter.Id, name, stats.BytesReceived, stats.BytesSent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new NetPoint("", "network", 0, 0);
        }
    }

    private NetworkInterface? SelectAdapter()
    {
        if (_adapter != null && DateTime.UtcNow - _adapterCheckedAt < TimeSpan.FromSeconds(5))
        {
            return _adapter;
        }

        _adapter = FindAdapter();
        _adapterCheckedAt = DateTime.UtcNow;
        return _adapter;
    }

    private static NetworkInterface? FindAdapter()
    {
        try
        {
            // 1.1.1.1 is the same in either byte order, so the route lookup stays unambiguous.
            var address = IPAddress.Parse("1.1.1.1").GetAddressBytes();
            var destination = (uint)(address[0] | (address[1] << 8) | (address[2] << 16) | (address[3] << 24));
            if (GetBestInterface(destination, out var index) == 0)
            {
                foreach (var candidate in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (!candidate.Supports(NetworkInterfaceComponent.IPv4))
                    {
                        continue;
                    }

                    var properties = candidate.GetIPProperties().GetIPv4Properties();
                    if (properties != null && properties.Index == (int)index)
                    {
                        return candidate;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        foreach (var candidate in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (candidate.OperationalStatus != OperationalStatus.Up
                || candidate.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var gateway in candidate.GetIPProperties().GatewayAddresses)
            {
                if (gateway.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static double Rate(long previous, long current, double seconds)
    {
        if (seconds <= 0 || current < previous)
        {
            return 0;
        }

        return (current - previous) / seconds;
    }

    private static ulong ToUInt64(FileTime value)
    {
        return ((ulong)value.High << 32) | value.Low;
    }

    private CoreTimes[]? ReadCoreTimes()
    {
        var count = Environment.ProcessorCount;
        if (count <= 0)
        {
            return null;
        }

        var stride = Marshal.SizeOf<ProcessorPerformance>();
        var length = stride * count;
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            var status = NtQuerySystemInformation(ProcessorPerformanceClass, buffer, length, out var returned);
            if (status == StatusInfoLengthMismatch && returned > length && returned <= stride * 256)
            {
                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
                length = returned;
                buffer = Marshal.AllocHGlobal(length);
                status = NtQuerySystemInformation(ProcessorPerformanceClass, buffer, length, out returned);
            }

            if (status != 0 || returned < stride)
            {
                return null;
            }

            var n = Math.Min(returned / stride, 256);
            var cores = new CoreTimes[n];
            for (var i = 0; i < n; i++)
            {
                var info = Marshal.PtrToStructure<ProcessorPerformance>(IntPtr.Add(buffer, i * stride));
                cores[i] = new CoreTimes(info.IdleTime, info.KernelTime, info.UserTime);
            }

            return cores;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static double[] PercentCores(CoreTimes[]? previous, CoreTimes[]? current)
    {
        if (previous == null || current == null || previous.Length != current.Length || current.Length == 0)
        {
            return Array.Empty<double>();
        }

        var result = new double[current.Length];
        for (var i = 0; i < current.Length; i++)
        {
            var idle = current[i].Idle - previous[i].Idle;
            var kernel = current[i].Kernel - previous[i].Kernel;
            var user = current[i].User - previous[i].User;
            var total = kernel + user;
            if (idle < 0 || kernel < 0 || user < 0 || total <= 0)
            {
                continue;
            }

            var busy = kernel > idle ? kernel - idle + user : user;
            result[i] = Math.Clamp(busy * 100.0 / total, 0, 100);
        }

        return result;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("ntdll.dll")]
    private static extern uint NtQuerySystemInformation(int systemInformationClass, IntPtr buffer, int length, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetBestInterface(uint destination, out uint interfaceIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User)
    {
        public ulong Total => Kernel + User;
        public ulong Busy => Kernel > Idle ? Kernel - Idle + User : User;
    }

    private readonly record struct RamPoint(double Percent, long Used, long Total, long CommitUsed, long CommitLimit);

    private readonly record struct DiskPoint(string Name, double UsedPercent, long FreeBytes, long TotalBytes);

    private readonly record struct CoreTimes(long Idle, long Kernel, long User);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPerformance
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public uint InterruptCount;
    }

    private readonly record struct NetPoint(string Id, string Name, long Received, long Sent);
}
