namespace WinMeter.Metrics;

public sealed record Snapshot(
    double CpuPercent,
    double RamPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    int LogicalProcessors,
    double DiskUsedPercent,
    string DiskName,
    long DiskFreeBytes,
    double DiskReadBps,
    double DiskWriteBps,
    double NetDownBps,
    double NetUpBps,
    string NetName,
    bool HasDelta,
    double[] CorePercents,
    long CommitUsedBytes,
    long CommitLimitBytes,
    long DiskTotalBytes,
    bool GpuReady,
    double GpuPercent);
