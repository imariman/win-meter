using System;
using System.Collections.Generic;
using System.ComponentModel;
using WinMeter.Metrics;

namespace WinMeter.Ui;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private string _cpuValue = "…";
    private string _cpuDetail = "measuring";
    private IReadOnlyList<double> _cpuSeries = Array.Empty<double>();
    private string _ramValue = "…";
    private string _ramDetail = "measuring";
    private IReadOnlyList<double> _ramSeries = Array.Empty<double>();
    private string _diskValue = "…";
    private string _diskDetail = "measuring";
    private IReadOnlyList<double> _diskSeries = Array.Empty<double>();
    private string _netValue = "…";
    private string _netDetail = "measuring";
    private IReadOnlyList<double> _netSeries = Array.Empty<double>();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CpuValue => _cpuValue;
    public string CpuDetail => _cpuDetail;
    public IReadOnlyList<double> CpuSeries => _cpuSeries;
    public string RamValue => _ramValue;
    public string RamDetail => _ramDetail;
    public IReadOnlyList<double> RamSeries => _ramSeries;
    public string DiskValue => _diskValue;
    public string DiskDetail => _diskDetail;
    public IReadOnlyList<double> DiskSeries => _diskSeries;
    public string NetValue => _netValue;
    public string NetDetail => _netDetail;
    public IReadOnlyList<double> NetSeries => _netSeries;

    public void Apply(Snapshot latest, IReadOnlyList<Snapshot> history)
    {
        var cpu = new double[history.Count];
        var ram = new double[history.Count];
        var disk = new double[history.Count];
        var net = new double[history.Count];
        for (var i = 0; i < history.Count; i++)
        {
            cpu[i] = history[i].CpuPercent;
            ram[i] = history[i].RamPercent;
            disk[i] = history[i].DiskReadBps + history[i].DiskWriteBps;
            net[i] = history[i].NetDownBps;
        }

        Set(ref _cpuValue, Format.Percent(latest.CpuPercent), nameof(CpuValue));
        Set(ref _cpuDetail, latest.LogicalProcessors + " logical processors", nameof(CpuDetail));
        Set(ref _cpuSeries, cpu, nameof(CpuSeries));
        Set(ref _ramValue, Format.Percent(latest.RamPercent), nameof(RamValue));
        Set(ref _ramDetail, Format.Bytes(latest.RamUsedBytes) + " / " + Format.Bytes(latest.RamTotalBytes), nameof(RamDetail));
        Set(ref _ramSeries, ram, nameof(RamSeries));
        Set(ref _diskValue, Format.Percent(latest.DiskUsedPercent), nameof(DiskValue));
        Set(ref _diskDetail, latest.DiskName + " · " + Format.Bytes(latest.DiskFreeBytes) + " free · read " + Format.Rate(latest.DiskReadBps) + " · write " + Format.Rate(latest.DiskWriteBps), nameof(DiskDetail));
        Set(ref _diskSeries, disk, nameof(DiskSeries));
        Set(ref _netValue, Format.Rate(latest.NetDownBps), nameof(NetValue));
        Set(ref _netDetail, latest.NetName + " · down " + Format.Rate(latest.NetDownBps) + " · up " + Format.Rate(latest.NetUpBps), nameof(NetDetail));
        Set(ref _netSeries, net, nameof(NetSeries));
    }

    private void Set<T>(ref T field, T value, string name)
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
