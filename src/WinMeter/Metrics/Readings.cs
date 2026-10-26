namespace WinMeter.Metrics;

public readonly record struct ProcessLoad(string Name, double CpuPercent, long MemoryBytes);

public readonly record struct DiskVolume(string Name, double UsedPercent, long FreeBytes, long TotalBytes);

public readonly record struct GpuAdapter(string Name, double Percent, long DedicatedUsedBytes, long DedicatedBytes, long SharedUsedBytes);

public readonly record struct ThermalZone(string Name, double Celsius);
