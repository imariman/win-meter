using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace WinMeter.Ui;

public partial class DashboardWindow : Window
{
    private const int ExtendedStyleIndex = -20;
    private const int ToolWindowStyle = 0x00000080;
    private const int CornerPreferenceAttribute = 33;
    private const int RoundCorners = 2;

    private bool _allowDeactivate;
    private DateTime _hiddenAt = DateTime.MinValue;
    private DateTime _suppressDismissUntil;

    public event EventHandler? Dismissed;

    public DashboardWindow()
    {
        InitializeComponent();
    }

    public void ToggleFromTray()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        if ((DateTime.UtcNow - _hiddenAt).TotalMilliseconds < 300)
        {
            return;
        }

        ShowNearCursor();
    }

    public void ShowFromMenu()
    {
        if (DataContext is DashboardViewModel model)
        {
            model.SetFocus(null);
        }

        if (IsVisible)
        {
            Activate();
            PlaceNearCursor();
            RefreshChrome();
            return;
        }

        ShowNearCursor();
    }

    public bool ShowMeter(MeterKind kind)
    {
        if (DataContext is not DashboardViewModel model)
        {
            return false;
        }

        if (IsVisible && model.Focus == kind)
        {
            Dismiss();
            return false;
        }

        model.SetFocus(kind);
        if (IsVisible)
        {
            Activate();
            PlaceNearCursor();
            return true;
        }

        ShowNearCursor();
        return true;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (IsVisible && _allowDeactivate)
        {
            PlaceNearCursor();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var preference = RoundCorners;
        DwmSetWindowAttribute(handle, CornerPreferenceAttribute, ref preference, sizeof(int));
        var style = GetWindowLongPtr(handle, ExtendedStyleIndex);
        SetWindowLongPtr(handle, ExtendedStyleIndex, new IntPtr(style.ToInt64() | ToolWindowStyle));
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (!_allowDeactivate || !IsVisible || DateTime.UtcNow < _suppressDismissUntil || PointerIsOverStrip())
        {
            return;
        }

        Dismiss();
    }

    private bool PointerIsOverStrip()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var under = WindowFromPoint(new NativePoint(cursor.X, cursor.Y));
        if (under == IntPtr.Zero)
        {
            return false;
        }

        var root = GetAncestor(under, 2);
        if (root == IntPtr.Zero)
        {
            root = under;
        }

        var self = new WindowInteropHelper(this).Handle;
        GetWindowThreadProcessId(root, out var processId);
        return root != self && processId == (uint)Environment.ProcessId;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismiss();
        }

        base.OnKeyDown(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
    }

    private void Dismiss()
    {
        if (!IsVisible)
        {
            return;
        }

        Hide();
        _hiddenAt = DateTime.UtcNow;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void ShowNearCursor()
    {
        _allowDeactivate = false;
        _suppressDismissUntil = DateTime.UtcNow.AddMilliseconds(500);
        PlaceNearCursor();
        Show();
        Activate();
        UpdateLayout();
        PlaceNearCursor();
        _allowDeactivate = true;
        RefreshChrome();
    }

    private void Range_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string tag || !int.TryParse(tag, out var seconds))
        {
            return;
        }

        if (DataContext is DashboardViewModel model)
        {
            model.SetRange(seconds);
        }

        RefreshChrome();
    }

    private void Strip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox box || box.Tag is not string name || !Enum.TryParse(name, out MeterKind kind))
        {
            return;
        }

        StripPreferences.Set(kind, box.IsChecked == true);
    }

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        AutoStart.Set(StartupBox.IsChecked == true);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current?.Shutdown();
    }

    private void RefreshChrome()
    {
        if (DataContext is not DashboardViewModel model)
        {
            return;
        }

        PaintRange(RangeMinuteButton, model.RangeSeconds == 60);
        PaintRange(RangeTenButton, model.RangeSeconds == 600);
        PaintRange(RangeHourButton, model.RangeSeconds == 3600);
        StartupBox.IsChecked = AutoStart.IsEnabled();
    }

    private static void PaintRange(System.Windows.Controls.Button button, bool selected)
    {
        button.Background = new System.Windows.Media.SolidColorBrush(selected
            ? System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)
            : System.Windows.Media.Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
        button.Foreground = new System.Windows.Media.SolidColorBrush(selected
            ? System.Windows.Media.Color.FromRgb(0xF4, 0xF6, 0xFB)
            : System.Windows.Media.Color.FromRgb(0xA7, 0xAE, 0xC0));
    }

    private void PlaceNearCursor()
    {
        UpdateLayout();
        var scale = Scale();
        var cursor = System.Windows.Forms.Cursor.Position;
        var area = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
        var width = ActualWidth > 1 ? ActualWidth : Width;
        var height = ActualHeight > 1 ? ActualHeight : 420;
        var x = cursor.X / scale.X - width + 28;
        var y = area.Bottom / scale.Y - height - 8;
        var left = area.Left / scale.X;
        var top = area.Top / scale.Y;
        var right = area.Right / scale.X;
        var bottom = area.Bottom / scale.Y;

        if (y < top)
        {
            y = cursor.Y / scale.Y + 12;
        }

        if (x < left + 8)
        {
            x = left + 8;
        }

        if (x + width > right - 8)
        {
            x = right - width - 8;
        }

        if (y + height > bottom - 8)
        {
            y = Math.Max(top + 8, bottom - height - 8);
        }

        Left = x;
        Top = y;
    }

    private (double X, double Y) Scale()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var transform = source.CompositionTarget.TransformToDevice;
            return (transform.M11, transform.M22);
        }

        using var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        return (graphics.DpiX / 96d, graphics.DpiY / 96d);
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

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr handle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr handle, int index, IntPtr value);
}
