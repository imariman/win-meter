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
    Gpu,
    Battery,
    Temperature
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
    private static readonly Brush BatteryBrush = Freeze("#FB923C");
    private static readonly Brush TempBrush = Freeze("#FB7185");
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
    private string _batteryValue = "…";
    private double _batteryPercent;
    private string _batteryLevel = LoadLevel.Normal;
    private string _batteryDetail = "measuring";
    private IReadOnlyList<double> _batterySeries = Array.Empty<double>();
    private string _tempValue = "…";
    private double _tempPercent;
    private string _tempLevel = LoadLevel.Normal;
    private string _tempDetail = "measuring";
    private IReadOnlyList<double> _tempSeries = Array.Empty<double>();
    private bool _hasBattery;
    private bool _hasTemperature;
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
    private IReadOnlyList<CoreLoad> _breakdown = Array.Empty<CoreLoad>();
    private string _breakdownTitle = "";
    private Brush _breakdownBrush = CpuBrush;
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StripBattery)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StripTemperature)));
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
    public string BatteryValue => _batteryValue;
    public double BatteryPercent => _batteryPercent;
    public string BatteryLevel => _batteryLevel;
    public string BatteryDetail => _batteryDetail;
    public IReadOnlyList<double> BatterySeries => _batterySeries;
    public string TempValue => _tempValue;
    public double TempPercent => _tempPercent;
    public string TempLevel => _tempLevel;
    public string TempDetail => _tempDetail;
    public IReadOnlyList<double> TempSeries => _tempSeries;
    public bool HasBattery => _hasBattery;
    public bool HasTemperature => _hasTemperature;
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
    public bool StripBattery
    {
        get => StripPreferences.Shows(MeterKind.Battery);
        set => StripPreferences.Set(MeterKind.Battery, value);
    }
    public bool StripTemperature
    {
        get => StripPreferences.Shows(MeterKind.Temperature);
        set => StripPreferences.Set(MeterKind.Temperature, value);
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
    public IReadOnlyList<CoreLoad> Breakdown => _breakdown;
    public string BreakdownTitle => _breakdownTitle;
    public Brush BreakdownBrush => _breakdownBrush;
    public bool ShowingBreakdown => _breakdown.Count > 0;
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
        var battery = new double[window.Length];
        var temp = new double[window.Length];
        for (var i = 0; i < window.Length; i++)
        {
            cpu[i] = window[i].CpuPercent;
            ram[i] = window[i].RamPercent;
            disk[i] = window[i].DiskReadBps + window[i].DiskWriteBps;
            net[i] = window[i].NetDownBps;
            gpu[i] = window[i].GpuPercent;
            battery[i] = window[i].BatteryPercent;
            temp[i] = window[i].TempCelsius;
        }

        var cpuDetail = latest.LogicalProcessors + " logical processors";
        if (latest.CpuMhz > 0)
        {
            cpuDetail = Format.Gigahertz(latest.CpuMhz) + " · " + cpuDetail;
        }

        Set(ref _cpuValue, Format.Percent(latest.CpuPercent), nameof(CpuValue));
        Set(ref _cpuPercent, Math.Clamp(latest.CpuPercent, 0, 100), nameof(CpuPercent));
        Set(ref _cpuLevel, LoadLevel.For(latest.CpuPercent), nameof(CpuLevel));
        Set(ref _cpuDetail, cpuDetail, nameof(CpuDetail));
        Set(ref _cpuSeries, cpu, nameof(CpuSeries));
        Set(ref _ramValue, Format.Percent(latest.RamPercent), nameof(RamValue));
        Set(ref _ramPercent, Math.Clamp(latest.RamPercent, 0, 100), nameof(RamPercent));
        Set(ref _ramLevel, LoadLevel.For(latest.RamPercent), nameof(RamLevel));
        Set(ref _ramDetail, Format.Bytes(latest.RamUsedBytes) + " / " + Format.Bytes(latest.RamTotalBytes), nameof(RamDetail));
        Set(ref _ramSeries, ram, nameof(RamSeries));
        var diskDetail = latest.DiskName + " · " + Format.Bytes(latest.DiskFreeBytes) + " free · read " + Format.Rate(latest.DiskReadBps) + " · write " + Format.Rate(latest.DiskWriteBps);
        if (latest.Volumes.Length > 1)
        {
            diskDetail += " · " + latest.Volumes.Length + " drives";
        }

        Set(ref _diskValue, Format.Percent(latest.DiskUsedPercent), nameof(DiskValue));
        Set(ref _diskPercent, Math.Clamp(latest.DiskUsedPercent, 0, 100), nameof(DiskPercent));
        Set(ref _diskLevel, LoadLevel.For(latest.DiskUsedPercent), nameof(DiskLevel));
        Set(ref _diskDetail, diskDetail, nameof(DiskDetail));
        Set(ref _diskSeries, disk, nameof(DiskSeries));
        var netDetail = latest.NetName + " · down " + Format.Rate(latest.NetDownBps) + " · up " + Format.Rate(latest.NetUpBps);
        if (latest.WifiSignal >= 0)
        {
            netDetail += " · signal " + Format.Percent(latest.WifiSignal);
        }

        Set(ref _netValue, Format.Rate(latest.NetDownBps), nameof(NetValue));
        Set(ref _netDetail, netDetail, nameof(NetDetail));
        Set(ref _netSeries, net, nameof(NetSeries));
        Set(ref _gpuValue, latest.GpuReady ? Format.Percent(latest.GpuPercent) : "—", nameof(GpuValue));
        Set(ref _gpuPercent, latest.GpuReady ? Math.Clamp(latest.GpuPercent, 0, 100) : 0, nameof(GpuPercent));
        Set(ref _gpuLevel, latest.GpuReady ? LoadLevel.For(latest.GpuPercent) : LoadLevel.Normal, nameof(GpuLevel));
        Set(ref _gpuDetail, GpuSummary(latest), nameof(GpuDetail));
        Set(ref _gpuSeries, gpu, nameof(GpuSeries));
        Set(ref _hasBattery, latest.BatteryPresent, nameof(HasBattery));
        Set(ref _batteryValue, latest.BatteryPresent ? Format.Percent(latest.BatteryPercent) : "—", nameof(BatteryValue));
        Set(ref _batteryPercent, latest.BatteryPresent ? Math.Clamp(latest.BatteryPercent, 0, 100) : 0, nameof(BatteryPercent));
        Set(ref _batteryLevel, latest.BatteryPresent ? LoadLevel.ForBattery(latest.BatteryPercent, latest.BatteryCharging) : LoadLevel.Normal, nameof(BatteryLevel));
        Set(ref _batteryDetail, BatterySummary(latest), nameof(BatteryDetail));
        Set(ref _batterySeries, battery, nameof(BatterySeries));
        var tempDetail = latest.TempName;
        if (latest.Zones.Length > 1)
        {
            tempDetail += " · " + latest.Zones.Length + " sensors";
        }

        Set(ref _hasTemperature, latest.TempReady, nameof(HasTemperature));
        Set(ref _tempValue, latest.TempReady ? Format.Degrees(latest.TempCelsius) : "—", nameof(TempValue));
        Set(ref _tempPercent, latest.TempReady ? Math.Clamp(latest.TempCelsius, 0, 100) : 0, nameof(TempPercent));
        Set(ref _tempLevel, latest.TempReady ? LoadLevel.For(latest.TempCelsius) : LoadLevel.Normal, nameof(TempLevel));
        Set(ref _tempDetail, latest.TempReady ? tempDetail : "unavailable", nameof(TempDetail));
        Set(ref _tempSeries, temp, nameof(TempSeries));
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
        var breakdown = new List<CoreLoad>();
        var breakdownTitle = "";
        var breakdownBrush = CpuBrush;

        if (latest != null && _focus != null)
        {
            switch (_focus.Value)
            {
                case MeterKind.Cpu:
                    heading = "CPU";
                    value = Format.Percent(latest.CpuPercent);
                    level = LoadLevel.For(latest.CpuPercent);
                    stroke = CpuBrush;
                    breakdownBrush = CpuBrush;
                    range = 100;
                    series = Series(item => item.CpuPercent);
                    if (latest.CpuMhz > 0)
                    {
                        rows.Add(new StatRow("Frequency", Format.Gigahertz(latest.CpuMhz)));
                    }

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

                    breakdownTitle = "Processes";
                    foreach (var process in latest.TopCpu)
                    {
                        var text = Format.PercentPrecise(process.CpuPercent);
                        if (process.MemoryBytes > 0)
                        {
                            text += " · " + Format.Bytes(process.MemoryBytes);
                        }

                        breakdown.Add(new CoreLoad(process.Name, process.CpuPercent, text));
                    }

                    break;
                case MeterKind.Memory:
                    heading = "Memory";
                    value = Format.Percent(latest.RamPercent);
                    level = LoadLevel.For(latest.RamPercent);
                    stroke = RamBrush;
                    breakdownBrush = RamBrush;
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

                    breakdownTitle = "Processes";
                    if (latest.RamTotalBytes > 0)
                    {
                        foreach (var process in latest.TopMemory)
                        {
                            breakdown.Add(new CoreLoad(process.Name, process.MemoryBytes * 100.0 / latest.RamTotalBytes, Format.Bytes(process.MemoryBytes)));
                        }
                    }

                    break;
                case MeterKind.Disk:
                    heading = "Disk";
                    value = Format.Percent(latest.DiskUsedPercent);
                    level = LoadLevel.For(latest.DiskUsedPercent);
                    caption = "Read + write";
                    stroke = DiskBrush;
                    breakdownBrush = DiskBrush;
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
                    if (latest.Volumes.Length > 1)
                    {
                        breakdownTitle = "Volumes";
                        foreach (var volume in latest.Volumes)
                        {
                            breakdown.Add(new CoreLoad(volume.Name, volume.UsedPercent, Format.Percent(volume.UsedPercent) + " · " + Format.Bytes(volume.FreeBytes) + " free"));
                        }
                    }

                    break;
                case MeterKind.Gpu:
                    heading = "GPU";
                    value = latest.GpuReady ? Format.Percent(latest.GpuPercent) : "—";
                    level = latest.GpuReady ? LoadLevel.For(latest.GpuPercent) : LoadLevel.Normal;
                    stroke = GpuBrush;
                    breakdownBrush = GpuBrush;
                    range = 100;
                    series = Series(item => item.GpuPercent);
                    var gpuName = latest.Adapters.Length > 0 && latest.Adapters[0].Name.Length > 0 ? latest.Adapters[0].Name : _machine.Graphics;
                    rows.Add(new StatRow("Adapter", gpuName));
                    rows.Add(new StatRow("Usage", latest.GpuReady ? Format.Percent(latest.GpuPercent) : "unavailable"));
                    if (latest.Adapters.Length > 0)
                    {
                        var main = latest.Adapters[0];
                        if (main.DedicatedBytes > 0 && main.DedicatedUsedBytes > 0)
                        {
                            rows.Add(new StatRow("Dedicated", Format.Bytes(main.DedicatedUsedBytes) + " / " + Format.Bytes(main.DedicatedBytes)));
                        }
                        else if (main.DedicatedBytes > 0)
                        {
                            rows.Add(new StatRow("Dedicated", Format.Bytes(main.DedicatedBytes)));
                        }
                        else if (main.DedicatedUsedBytes > 0)
                        {
                            rows.Add(new StatRow("Dedicated", Format.Bytes(main.DedicatedUsedBytes)));
                        }

                        if (main.SharedUsedBytes > 0)
                        {
                            rows.Add(new StatRow("Shared", Format.Bytes(main.SharedUsedBytes)));
                        }
                    }

                    if (latest.Adapters.Length > 1)
                    {
                        breakdownTitle = "Adapters";
                        foreach (var adapter in latest.Adapters)
                        {
                            breakdown.Add(new CoreLoad(adapter.Name, adapter.Percent, AdapterText(adapter)));
                        }
                    }

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
                    if (latest.WifiSignal >= 0)
                    {
                        rows.Add(new StatRow("Signal", Format.Percent(latest.WifiSignal)));
                        if (latest.WifiSsid.Length > 0)
                        {
                            rows.Add(new StatRow("Wi-Fi", latest.WifiSsid));
                        }
                    }

                    break;
                case MeterKind.Battery:
                    heading = "Battery";
                    value = latest.BatteryPresent ? Format.Percent(latest.BatteryPercent) : "—";
                    level = latest.BatteryPresent ? LoadLevel.ForBattery(latest.BatteryPercent, latest.BatteryCharging) : LoadLevel.Normal;
                    stroke = BatteryBrush;
                    range = 100;
                    series = Series(item => item.BatteryPercent);
                    rows.Add(new StatRow("Power", latest.BatteryPresent ? PowerSource(latest) : "not present"));
                    if (latest.BatteryPresent && latest.BatterySeconds >= 0 && !latest.BatteryCharging)
                    {
                        rows.Add(new StatRow("Remaining", Format.Remaining(latest.BatterySeconds)));
                    }

                    break;
                case MeterKind.Temperature:
                    heading = "Temperature";
                    value = latest.TempReady ? Format.Degrees(latest.TempCelsius) : "—";
                    level = latest.TempReady ? LoadLevel.For(latest.TempCelsius) : LoadLevel.Normal;
                    stroke = TempBrush;
                    range = 100;
                    series = Series(item => item.TempCelsius);
                    if (!latest.TempReady)
                    {
                        rows.Add(new StatRow("Sensor", "unavailable"));
                    }

                    foreach (var zone in latest.Zones)
                    {
                        rows.Add(new StatRow(zone.Name, Format.Celsius(zone.Celsius)));
                    }

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
        Set(ref _breakdown, breakdown, nameof(Breakdown));
        Set(ref _breakdownTitle, breakdownTitle, nameof(BreakdownTitle));
        Set(ref _breakdownBrush, breakdownBrush, nameof(BreakdownBrush));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowingCores)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowingBreakdown)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowingCaption)));
    }

    private string GpuSummary(Snapshot latest)
    {
        if (latest.Adapters.Length == 0)
        {
            return _machine.Graphics;
        }

        var main = latest.Adapters[0];
        var name = main.Name.Length > 0 ? main.Name : _machine.Graphics;
        if (main.DedicatedBytes > 0 && main.DedicatedUsedBytes > 0)
        {
            return name + " · " + Format.Bytes(main.DedicatedUsedBytes) + " / " + Format.Bytes(main.DedicatedBytes);
        }

        if (main.DedicatedBytes > 0)
        {
            return name + " · " + Format.Bytes(main.DedicatedBytes);
        }

        return latest.Adapters.Length > 1 ? name + " · " + latest.Adapters.Length + " adapters" : name;
    }

    private static string BatterySummary(Snapshot latest)
    {
        if (!latest.BatteryPresent)
        {
            return "not present";
        }

        var text = PowerSource(latest);
        if (!latest.BatteryCharging && latest.BatterySeconds >= 0)
        {
            text += " · " + Format.Remaining(latest.BatterySeconds);
        }

        return text;
    }

    private static string PowerSource(Snapshot latest)
    {
        if (latest.BatteryCharging)
        {
            return "Charging";
        }

        return latest.BatteryOnAc ? "Plugged in" : "On battery";
    }

    private static string AdapterText(GpuAdapter adapter)
    {
        var text = Format.Percent(adapter.Percent);
        if (adapter.DedicatedBytes > 0 && adapter.DedicatedUsedBytes > 0)
        {
            return text + " · " + Format.Bytes(adapter.DedicatedUsedBytes) + " / " + Format.Bytes(adapter.DedicatedBytes);
        }

        if (adapter.DedicatedBytes > 0)
        {
            return text + " · " + Format.Bytes(adapter.DedicatedBytes);
        }

        if (adapter.DedicatedUsedBytes > 0)
        {
            return text + " · " + Format.Bytes(adapter.DedicatedUsedBytes);
        }

        return text;
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
