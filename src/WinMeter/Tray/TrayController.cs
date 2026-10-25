using System;
using System.Drawing;
using System.Windows.Forms;
using WinMeter.Metrics;
using WinMeter.Ui;

namespace WinMeter.Tray;

internal sealed class TrayController : IDisposable
{
    private readonly SystemSampler _sampler;
    private readonly DashboardViewModel _model = new();
    private readonly DashboardWindow _dashboard = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly Slot[] _slots;
    private bool _disposed;

    public TrayController(SystemSampler sampler)
    {
        _sampler = sampler;
        _dashboard.DataContext = _model;
        _slots = new[]
        {
            MakeSlot(),
            MakeSlot(),
            MakeSlot(),
            MakeSlot()
        };

        var open = new ToolStripMenuItem("Open");
        open.Click += (_, _) => _dashboard.ShowFromMenu();
        var startup = new ToolStripMenuItem("Start with Windows");
        startup.Click += (_, _) => AutoStart.Set(!AutoStart.IsEnabled());
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => System.Windows.Application.Current?.Shutdown();
        _menu.Items.Add(open);
        _menu.Items.Add(startup);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exit);
        _menu.Opening += (_, _) => startup.Checked = AutoStart.IsEnabled();

        foreach (var slot in _slots)
        {
            slot.Icon.ContextMenuStrip = _menu;
            slot.Icon.MouseUp += (_, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    _dashboard.ToggleFromTray();
                }
            };
        }
    }

    public void Start()
    {
        Paint(null);
        foreach (var slot in _slots)
        {
            slot.Icon.Visible = true;
        }

        _sampler.Updated += OnSample;
        _sampler.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sampler.Updated -= OnSample;
        foreach (var slot in _slots)
        {
            slot.Icon.Visible = false;
            slot.Icon.Dispose();
            slot.Image?.Dispose();
        }

        _menu.Dispose();
        _dashboard.Close();
    }

    private void OnSample(object? sender, Snapshot snapshot)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.BeginInvoke(new Action(() =>
        {
            if (_disposed)
            {
                return;
            }

            _model.Apply(snapshot, _sampler.CopyHistory());
            Paint(snapshot);
        }));
    }

    private void Paint(Snapshot? snapshot)
    {
        if (snapshot == null || !snapshot.HasDelta)
        {
            Set(_slots[0], "CPU", "…", IconFactory.ColorForPercent(0), "Measuring CPU");
            Set(_slots[1], "RAM", "…", IconFactory.ColorForPercent(0), "Measuring memory");
            Set(_slots[2], "DSK", "…", IconFactory.ColorForPercent(0), "Measuring disk");
            Set(_slots[3], "NET", "…", Color.FromArgb(214, 196, 255), "Measuring network");
            return;
        }

        var cpu = ((int)Math.Round(snapshot.CpuPercent)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var ram = ((int)Math.Round(snapshot.RamPercent)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var disk = ((int)Math.Round(snapshot.DiskUsedPercent)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Set(_slots[0], "CPU", cpu, IconFactory.ColorForPercent(snapshot.CpuPercent), "CPU " + Format.Percent(snapshot.CpuPercent));
        Set(_slots[1], "RAM", ram, IconFactory.ColorForPercent(snapshot.RamPercent), "Memory " + Format.Percent(snapshot.RamPercent) + " · " + Format.Bytes(snapshot.RamUsedBytes) + " / " + Format.Bytes(snapshot.RamTotalBytes));
        Set(_slots[2], "DSK", disk, IconFactory.ColorForPercent(snapshot.DiskUsedPercent), "Disk " + snapshot.DiskName + " " + Format.Percent(snapshot.DiskUsedPercent) + " · read " + Format.Rate(snapshot.DiskReadBps) + " · write " + Format.Rate(snapshot.DiskWriteBps));
        Set(_slots[3], "NET", Format.CompactRate(snapshot.NetDownBps), Color.FromArgb(214, 196, 255), "Network " + snapshot.NetName + " · down " + Format.Rate(snapshot.NetDownBps) + " · up " + Format.Rate(snapshot.NetUpBps));
    }

    private static void Set(Slot slot, string label, string value, Color color, string tooltip)
    {
        var stamp = label + "|" + value + "|" + color.ToArgb();
        if (!string.Equals(slot.Stamp, stamp, StringComparison.Ordinal))
        {
            var icon = IconFactory.Create(label, value, color);
            var previous = slot.Image;
            slot.Icon.Icon = icon;
            slot.Image = icon;
            slot.Stamp = stamp;
            previous?.Dispose();
        }

        slot.Icon.Text = tooltip.Length > 120 ? tooltip.Substring(0, 120) : tooltip;
    }

    private static Slot MakeSlot()
    {
        return new Slot { Icon = new NotifyIcon() };
    }

    private sealed class Slot
    {
        public NotifyIcon Icon { get; set; } = new();
        public Icon? Image { get; set; }
        public string? Stamp { get; set; }
    }
}
