using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using WinMeter.Metrics;

namespace WinMeter.Ui;

public enum MeterKind
{
    Cpu,
    Memory,
    Disk,
    Network,
    Gpu
}

public sealed class StatRow
{
    public StatRow(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public string Value { get; }
}

public sealed class CoreLoad
{
    public CoreLoad(string label, double percent, string text)
    {
        Label = label;
        Percent = percent;
        Text = text;
        Level = LoadLevel.For(percent);
    }

    public string Label { get; }

    public double Percent { get; }

    public string Text { get; }

    public string Level { get; }
}

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    // Metric accents; keep in sync with DashboardWindow.xaml and TaskbarStrip.xaml.
    private static readonly Brush CpuBrush = Freeze("#60A5FA");
    private static readonly Brush RamBrush = Freeze("#4ADE80");
    private static readonly Brush DiskBrush = Freeze("#2DD4BF");
    private static readonly Brush NetBrush = Freeze("#A78BFA");
    private static readonly Brush GpuBrush = Freeze("#F472B6");
    private readonly MachineProfile _machine = MachineProfile.Read();

    private string _cpuValue = "…";
    private double _cpuPercent;
    private string _cpuLevel = LoadLevel.Normal;
    private string _cpuDetail = "measuring";
    private IReadOnlyList<double> _cpuSeries = Array.Empty<double>();
    private string _ramValue = "…";
    private double _ramPercent;
    private string _ramLevel = LoadLevel.Normal;
    private string _ramDetail = "measuring";
    private IReadOnlyList<double> _ramSeries = Array.Empty<double>();
    private string _diskValue = "…";
    private double _diskPercent;
    private string _diskLevel = LoadLevel.Normal;
    private string _diskDetail = "measuring";
    private IReadOnlyList<double> _diskSeries = Array.Empty<double>();
    private string _netValue = "…";
    private string _netDetail = "measuring";
    private IReadOnlyList<double> _netSeries = Array.Empty<double>();
    private string _gpuValue = "…";
    private double _gpuPercent;
    private string _gpuLevel = LoadLevel.Normal;
    private string _gpuDetail = "measuring";
    private IReadOnlyList<double> _gpuSeries = Array.Empty<double>();
    private string _memoryTotal = "…";
    private int _historySeconds = 60;
    private string _heading = "WinMeter";
    private string _focusValue = "…";
    private string _focusLevel = LoadLevel.Normal;
    private string _chartCaption = "";
    private Brush _focusStroke = CpuBrush;
    private double _focusRangeMax = 100;
    private IReadOnlyList<double> _focusSeries = Array.Empty<double>();
    private IReadOnlyList<StatRow> _rows = Array.Empty<StatRow>();
    private IReadOnlyList<CoreLoad> _cores = Array.Empty<CoreLoad>();
    private Snapshot? _latest;
    private IReadOnlyList<Snapshot> _history = Array.Empty<Snapshot>();
    private long _firstSequence;
    private long _seriesOrigin;
    private MeterKind? _focus;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DashboardViewModel()
    {
        StripPreferences.Changed += (_, _) =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StripCpu)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StripMemory)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StripGpu)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StripDisk)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StripNetwork)));
        };
    }

    public string CpuValue => _cpuValue;
    public double CpuPercent => _cpuPercent;
    public string CpuLevel => _cpuLevel;
    public string CpuDetail => _cpuDetail;
    public IReadOnlyList<double> CpuSeries => _cpuSeries;
    public string RamValue => _ramValue;
    public double RamPercent => _ramPercent;
    public string RamLevel => _ramLevel;
    public string RamDetail => _ramDetail;
    public IReadOnlyList<double> RamSeries => _ramSeries;
    public string DiskValue => _diskValue;
    public double DiskPercent => _diskPercent;
    public string DiskLevel => _diskLevel;
    public string DiskDetail => _diskDetail;
    public IReadOnlyList<double> DiskSeries => _diskSeries;
    public string NetValue => _netValue;
    public string NetDetail => _netDetail;
    public IReadOnlyList<double> NetSeries => _netSeries;
    public string GpuValue => _gpuValue;
    public double GpuPercent => _gpuPercent;
    public string GpuLevel => _gpuLevel;
    public string GpuDetail => _gpuDetail;
    public IReadOnlyList<double> GpuSeries => _gpuSeries;
    public string PcModel => _machine.Model;
    public string PcProcessor => _machine.Processor;
    public string PcGraphics => _machine.Graphics;
    public string PcWindows => _machine.Windows;
    public string PcMemory => _memoryTotal;
    public string RangeLabel
    {
        get
        {
            var label = _historySeconds switch
            {
                600 => "Last 10 minutes",
                3600 => "Last hour",
                _ => "Last minute"
            };
            var collected = _history.Count;
            if (collected == 0)
            {
                return label + " · collecting";
            }

            return collected < _historySeconds ? label + " · " + Duration(collected) + " so far" : label;
        }
    }
    public int RangeSeconds => _historySeconds;
    public int SeriesCapacity => _historySeconds;
    public long SeriesOrigin => _seriesOrigin;
    public bool StripCpu
    {
        get => StripPreferences.Shows(MeterKind.Cpu);
        set => StripPreferences.Set(MeterKind.Cpu, value);
    }
    public bool StripMemory
    {
        get => StripPreferences.Shows(MeterKind.Memory);
        set => StripPreferences.Set(MeterKind.Memory, value);
    }
    public bool StripGpu
    {
        get => StripPreferences.Shows(MeterKind.Gpu);
        set => StripPreferences.Set(MeterKind.Gpu, value);
    }
    public bool StripDisk
    {
        get => StripPreferences.Shows(MeterKind.Disk);
        set => StripPreferences.Set(MeterKind.Disk, value);
    }
    public bool StripNetwork
    {
        get => StripPreferences.Shows(MeterKind.Network);
        set => StripPreferences.Set(MeterKind.Network, value);
    }
    public string Heading => _heading;
    public string FocusValue => _focusValue;
    public string FocusLevel => _focusLevel;
    public string ChartCaption => _chartCaption;
    public Brush FocusStroke => _focusStroke;
    public double FocusRangeMax => _focusRangeMax;
    public IReadOnlyList<double> FocusSeries => _focusSeries;
    public IReadOnlyList<StatRow> Rows => _rows;
    public IReadOnlyList<CoreLoad> Cores => _cores;
    public MeterKind? Focus => _focus;
    public bool ShowingAll => _focus == null;
    public bool ShowingOne => _focus != null;
    public bool ShowingCores => _focus == MeterKind.Cpu && _cores.Count > 0;
    public bool ShowingCaption => _chartCaption.Length > 0;

    public void Apply(Snapshot latest, IReadOnlyList<Snapshot> history, long firstSequence)
    {
        _latest = latest;
        _history = history;
        _firstSequence = firstSequence;
        var window = Window();
        Set(ref _seriesOrigin, firstSequence + history.Count - window.Length, nameof(SeriesOrigin));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RangeLabel)));
        var cpu = new double[window.Length];
        var ram = new double[window.Length];
        var disk = new double[window.Length];
        var net = new double[window.Length];
        var gpu = new double[window.Length];
        for (var i = 0; i < window.Length; i++)
        {
            cpu[i] = window[i].CpuPercent;
            ram[i] = window[i].RamPercent;
            disk[i] = window[i].DiskReadBps + window[i].DiskWriteBps;
            net[i] = window[i].NetDownBps;
            gpu[i] = window[i].GpuPercent;
        }

        Set(ref _cpuValue, Format.Percent(latest.CpuPercent), nameof(CpuValue));
        Set(ref _cpuPercent, Math.Clamp(latest.CpuPercent, 0, 100), nameof(CpuPercent));
        Set(ref _cpuLevel, LoadLevel.For(latest.CpuPercent), nameof(CpuLevel));
        Set(ref _cpuDetail, latest.LogicalProcessors + " logical processors", nameof(CpuDetail));
        Set(ref _cpuSeries, cpu, nameof(CpuSeries));
        Set(ref _ramValue, Format.Percent(latest.RamPercent), nameof(RamValue));
        Set(ref _ramPercent, Math.Clamp(latest.RamPercent, 0, 100), nameof(RamPercent));
        Set(ref _ramLevel, LoadLevel.For(latest.RamPercent), nameof(RamLevel));
        Set(ref _ramDetail, Format.Bytes(latest.RamUsedBytes) + " / " + Format.Bytes(latest.RamTotalBytes), nameof(RamDetail));
        Set(ref _ramSeries, ram, nameof(RamSeries));
        Set(ref _diskValue, Format.Percent(latest.DiskUsedPercent), nameof(DiskValue));
        Set(ref _diskPercent, Math.Clamp(latest.DiskUsedPercent, 0, 100), nameof(DiskPercent));
        Set(ref _diskLevel, LoadLevel.For(latest.DiskUsedPercent), nameof(DiskLevel));
        Set(ref _diskDetail, latest.DiskName + " · " + Format.Bytes(latest.DiskFreeBytes) + " free · read " + Format.Rate(latest.DiskReadBps) + " · write " + Format.Rate(latest.DiskWriteBps), nameof(DiskDetail));
        Set(ref _diskSeries, disk, nameof(DiskSeries));
        Set(ref _netValue, Format.Rate(latest.NetDownBps), nameof(NetValue));
        Set(ref _netDetail, latest.NetName + " · down " + Format.Rate(latest.NetDownBps) + " · up " + Format.Rate(latest.NetUpBps), nameof(NetDetail));
        Set(ref _netSeries, net, nameof(NetSeries));
        Set(ref _gpuValue, latest.GpuReady ? Format.Percent(latest.GpuPercent) : "—", nameof(GpuValue));
        Set(ref _gpuPercent, latest.GpuReady ? Math.Clamp(latest.GpuPercent, 0, 100) : 0, nameof(GpuPercent));
        Set(ref _gpuLevel, latest.GpuReady ? LoadLevel.For(latest.GpuPercent) : LoadLevel.Normal, nameof(GpuLevel));
        Set(ref _gpuDetail, _machine.Graphics, nameof(GpuDetail));
        Set(ref _gpuSeries, gpu, nameof(GpuSeries));
        Set(ref _memoryTotal, Format.Bytes(latest.RamTotalBytes), nameof(PcMemory));
        if (_focus != null)
        {
            FillFocus();
        }
    }

    public void SetRange(int seconds)
    {
        if (seconds != 60 && seconds != 600 && seconds != 3600)
        {
            return;
        }

        _historySeconds = seconds;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RangeLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RangeSeconds)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeriesCapacity)));
        if (_latest != null)
        {
            Apply(_latest, _history, _firstSequence);
        }
    }

    public void SetFocus(MeterKind? kind)
    {
        _focus = kind;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Focus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowingAll)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowingOne)));
        FillFocus();
    }

    private void FillFocus()
    {
        var latest = _latest;
        var heading = "WinMeter";
        var value = "…";
        var level = LoadLevel.Normal;
        var caption = "";
        var stroke = CpuBrush;
        var range = 100d;
        IReadOnlyList<double> series = Array.Empty<double>();
        var rows = new List<StatRow>();
        var cores = new List<CoreLoad>();

        if (latest != null && _focus != null)
        {
            switch (_focus.Value)
            {
                case MeterKind.Cpu:
                    heading = "CPU";
                    value = Format.Percent(latest.CpuPercent);
                    level = LoadLevel.For(latest.CpuPercent);
                    stroke = CpuBrush;
                    range = 100;
                    series = Series(item => item.CpuPercent);
                    rows.Add(new StatRow("Logical processors", latest.LogicalProcessors.ToString()));
                    var busiest = Busiest(latest.CorePercents);
                    if (busiest >= 0)
                    {
                        rows.Add(new StatRow("Busiest core", "Core " + (busiest + 1) + " · " + Format.Percent(latest.CorePercents[busiest])));
                    }

                    for (var i = 0; i < latest.CorePercents.Length; i++)
                    {
                        cores.Add(new CoreLoad((i + 1).ToString(), latest.CorePercents[i], Format.Percent(latest.CorePercents[i])));
                    }

                    break;
                case MeterKind.Memory:
                    heading = "Memory";
                    value = Format.Percent(latest.RamPercent);
                    level = LoadLevel.For(latest.RamPercent);
                    stroke = RamBrush;
                    range = 100;
                    series = Series(item => item.RamPercent);
                    var available = Math.Max(0, latest.RamTotalBytes - latest.RamUsedBytes);
                    rows.Add(new StatRow("Used", Format.Bytes(latest.RamUsedBytes)));
                    rows.Add(new StatRow("Available", Format.Bytes(available)));
                    rows.Add(new StatRow("Total", Format.Bytes(latest.RamTotalBytes)));
                    if (latest.CommitLimitBytes > 0)
                    {
                        rows.Add(new StatRow("Committed", Format.Bytes(latest.CommitUsedBytes) + " / " + Format.Bytes(latest.CommitLimitBytes)));
                    }

                    break;
                case MeterKind.Disk:
                    heading = "Disk";
                    value = Format.Percent(latest.DiskUsedPercent);
                    level = LoadLevel.For(latest.DiskUsedPercent);
                    caption = "Read + write";
                    stroke = DiskBrush;
                    range = 0;
                    series = Series(item => item.DiskReadBps + item.DiskWriteBps);
                    rows.Add(new StatRow("Volume", latest.DiskName));
                    if (latest.DiskTotalBytes > latest.DiskFreeBytes)
                    {
                        rows.Add(new StatRow("Used", Format.Bytes(latest.DiskTotalBytes - latest.DiskFreeBytes)));
                    }

                    rows.Add(new StatRow("Free", Format.Bytes(latest.DiskFreeBytes)));
                    if (latest.DiskTotalBytes > 0)
                    {
                        rows.Add(new StatRow("Capacity", Format.Bytes(latest.DiskTotalBytes)));
                    }

                    rows.Add(new StatRow("Read", Format.Rate(latest.DiskReadBps)));
                    rows.Add(new StatRow("Write", Format.Rate(latest.DiskWriteBps)));
                    break;
                case MeterKind.Gpu:
                    heading = "GPU";
                    value = latest.GpuReady ? Format.Percent(latest.GpuPercent) : "—";
                    level = latest.GpuReady ? LoadLevel.For(latest.GpuPercent) : LoadLevel.Normal;
                    stroke = GpuBrush;
                    range = 100;
                    series = Series(item => item.GpuPercent);
                    rows.Add(new StatRow("Adapter", _machine.Graphics));
                    rows.Add(new StatRow("Usage", latest.GpuReady ? Format.Percent(latest.GpuPercent) : "unavailable"));
                    break;
                case MeterKind.Network:
                    heading = "Network";
                    value = Format.Rate(latest.NetDownBps);
                    caption = "Download";
                    stroke = NetBrush;
                    range = 0;
                    series = Series(item => item.NetDownBps);
                    rows.Add(new StatRow("Adapter", latest.NetName));
                    rows.Add(new StatRow("Download", Format.Rate(latest.NetDownBps)));
                    rows.Add(new StatRow("Upload", Format.Rate(latest.NetUpBps)));
                    break;
            }
        }

        Set(ref _heading, heading, nameof(Heading));
        Set(ref _focusValue, value, nameof(FocusValue));
        Set(ref _focusLevel, level, nameof(FocusLevel));
        Set(ref _chartCaption, caption, nameof(ChartCaption));
        Set(ref _focusStroke, stroke, nameof(FocusStroke));
        Set(ref _focusRangeMax, range, nameof(FocusRangeMax));
        Set(ref _focusSeries, series, nameof(FocusSeries));
        Set(ref _rows, rows, nameof(Rows));
        Set(ref _cores, cores, nameof(Cores));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowingCores)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowingCaption)));
    }

    private Snapshot[] Window()
    {
        var count = _history.Count;
        var take = Math.Min(_historySeconds, count);
        var slice = new Snapshot[take];
        var start = count - take;
        for (var i = 0; i < take; i++)
        {
            slice[i] = _history[start + i];
        }

        return slice;
    }

    private double[] Series(Func<Snapshot, double> select)
    {
        var history = Window();
        var series = new double[history.Length];
        for (var i = 0; i < history.Length; i++)
        {
            series[i] = select(history[i]);
        }

        return series;
    }

    private static string Duration(int seconds)
    {
        return seconds < 60
            ? seconds.ToString(System.Globalization.CultureInfo.CurrentCulture) + " s"
            : (seconds / 60).ToString(System.Globalization.CultureInfo.CurrentCulture) + " min";
    }

    private static int Busiest(double[] cores)
    {
        var index = -1;
        var max = -1d;
        for (var i = 0; i < cores.Length; i++)
        {
            if (cores[i] > max)
            {
                max = cores[i];
                index = i;
            }
        }

        return index;
    }

    private static Brush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private void Set<T>(ref T field, T value, string name)
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
