using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WinMeter.Metrics;

namespace WinMeter.Ui;

public partial class TaskbarStrip : Window
{
    private const int ExtendedStyleIndex = -20;
    private const int ToolWindowStyle = 0x00000080;
    private const int NoActivateStyle = 0x08000000;
    private const int MouseActivateMessage = 0x0021;
    private const int MouseActivateNoActivate = 3;
    private const uint NoSize = 0x0001;
    private const uint NoMove = 0x0002;
    private const uint NoActivate = 0x0010;

    private delegate IntPtr WndProcDelegate(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private static readonly Brush NormalValue = Frozen(Color.FromRgb(0xF8, 0xFA, 0xFC));
    private static readonly Brush HighValue = Frozen(Color.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly Brush CriticalValue = Frozen(Color.FromRgb(0xF8, 0x71, 0x71));

    private WndProcDelegate? _wndProc;
    private IntPtr _previousProc;

    private Snapshot? _shown;

    public event EventHandler? OpenRequested;

    public event EventHandler<MeterKind>? MeterRequested;

    public event EventHandler? MenuRequested;

    public TaskbarStrip()
    {
        InitializeComponent();
    }

    public void Apply(Snapshot snapshot)
    {
        _shown = snapshot;
        CpuValue.Text = Format.Percent(snapshot.CpuPercent);
        RamValue.Text = Format.Percent(snapshot.RamPercent);
        SsdValue.Text = Format.Percent(snapshot.DiskUsedPercent);
        GpuValue.Text = snapshot.GpuReady ? Format.Percent(snapshot.GpuPercent) : "—";
        Paint(CpuValue, snapshot.CpuPercent);
        Paint(RamValue, snapshot.RamPercent);
        Paint(SsdValue, snapshot.DiskUsedPercent);
        Paint(GpuValue, snapshot.GpuReady ? snapshot.GpuPercent : 0);
        NetValue.Text = Format.CompactRate(snapshot.NetDownBps);
        if (snapshot.BatteryPresent)
        {
            BatValue.Text = Format.Percent(snapshot.BatteryPercent);
            PaintBattery(BatValue, snapshot.BatteryPercent, snapshot.BatteryCharging);
        }

        if (snapshot.TempReady)
        {
            TmpValue.Text = Format.Degrees(snapshot.TempCelsius);
            Paint(TmpValue, snapshot.TempCelsius);
        }

        ApplyVisibility();
    }

    public void ApplyVisibility()
    {
        CpuHit.Visibility = Shown(MeterKind.Cpu);
        RamHit.Visibility = Shown(MeterKind.Memory);
        GpuHit.Visibility = Shown(MeterKind.Gpu);
        SsdHit.Visibility = Shown(MeterKind.Disk);
        NetHit.Visibility = Shown(MeterKind.Network);
        BatHit.Visibility = _shown?.BatteryPresent == true && StripPreferences.Shows(MeterKind.Battery) ? Visibility.Visible : Visibility.Collapsed;
        TmpHit.Visibility = _shown?.TempReady == true && StripPreferences.Shows(MeterKind.Temperature) ? Visibility.Visible : Visibility.Collapsed;
        UpdateLayout();
        Place();
    }

    private static void Paint(TextBlock value, double percent)
    {
        value.Foreground = LoadLevel.For(percent) switch
        {
            LoadLevel.Critical => CriticalValue,
            LoadLevel.High => HighValue,
            _ => NormalValue
        };
    }

    private static void PaintBattery(TextBlock value, double percent, bool charging)
    {
        value.Foreground = LoadLevel.ForBattery(percent, charging) switch
        {
            LoadLevel.Critical => CriticalValue,
            LoadLevel.High => HighValue,
            _ => NormalValue
        };
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Visibility Shown(MeterKind kind)
    {
        return StripPreferences.Shows(kind) ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Place()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero)
        {
            return;
        }

        var notify = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notify == IntPtr.Zero || !GetWindowRect(taskbar, out var bar) || !GetWindowRect(notify, out var tray))
        {
            return;
        }

        var barWidth = bar.Right - bar.Left;
        var barHeight = bar.Bottom - bar.Top;
        var screen = System.Windows.Forms.Screen.FromHandle(taskbar).Bounds;
        if (barWidth < barHeight || barHeight < 20 || bar.Top >= screen.Bottom - 4)
        {
            Hide();
            return;
        }

        UpdateLayout();
        var scale = Scale();
        var width = ActualWidth > 1 ? ActualWidth : 320;
        var height = Height;
        var x = tray.Left / scale.X - width - 6;
        var y = bar.Top / scale.Y + ((barHeight / scale.Y) - height) / 2;
        var minimumX = bar.Left / scale.X + 4;
        if (x < minimumX)
        {
            x = minimumX;
        }

        Left = x;
        Top = y;
        if (!IsVisible)
        {
            Show();
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, NoMove | NoSize | NoActivate);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, ExtendedStyleIndex);
        SetWindowLongPtr(handle, ExtendedStyleIndex, new IntPtr(style.ToInt64() | ToolWindowStyle | NoActivateStyle));
        _wndProc = FilterActivation;
        _previousProc = SetWindowLongPtr(handle, -4, Marshal.GetFunctionPointerForDelegate(_wndProc));
        HwndSource.FromHwnd(handle)?.AddHook(KeepClickWithoutActivation);
    }

    private IntPtr FilterActivation(IntPtr window, int message, IntPtr wParam, IntPtr lParam)
    {
        if (message == MouseActivateMessage)
        {
            return new IntPtr(MouseActivateNoActivate);
        }

        return CallWindowProc(_previousProc, window, message, wParam, lParam);
    }

    private static IntPtr KeepClickWithoutActivation(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != MouseActivateMessage)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(MouseActivateNoActivate);
    }

    public void Highlight(MeterKind? kind)
    {
        CpuHighlight.Opacity = kind == MeterKind.Cpu ? 1 : 0;
        RamHighlight.Opacity = kind == MeterKind.Memory ? 1 : 0;
        SsdHighlight.Opacity = kind == MeterKind.Disk ? 1 : 0;
        GpuHighlight.Opacity = kind == MeterKind.Gpu ? 1 : 0;
        NetHighlight.Opacity = kind == MeterKind.Network ? 1 : 0;
        BatHighlight.Opacity = kind == MeterKind.Battery ? 1 : 0;
        TmpHighlight.Opacity = kind == MeterKind.Temperature ? 1 : 0;
    }

    private void Logo_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Highlight(null);
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Segment_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { Tag: string name } && Enum.TryParse(name, out MeterKind kind))
        {
            MeterRequested?.Invoke(this, kind);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!e.Handled)
        {
            OpenRequested?.Invoke(this, EventArgs.Empty);
        }

        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        MenuRequested?.Invoke(this, EventArgs.Empty);
        base.OnMouseRightButtonUp(e);
    }

    private (double X, double Y) Scale()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var transform = source.CompositionTarget.TransformToDevice;
            return (transform.M11 == 0 ? 1 : transform.M11, transform.M22 == 0 ? 1 : transform.M22);
        }

        return (1, 1);
    }

    private static IntPtr GetWindowLongPtr(IntPtr handle, int index)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(handle, index) : new IntPtr(GetWindowLong32(handle, index));
    }

    private static IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(handle, index, value)
            : new IntPtr(SetWindowLong32(handle, index, value.ToInt32()));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out Rect32 rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr previous, IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr handle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr handle, int index, IntPtr value);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
