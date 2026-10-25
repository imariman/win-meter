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
        if (IsVisible)
        {
            Activate();
            return;
        }

        ShowNearCursor();
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
        if (!_allowDeactivate || !IsVisible)
        {
            return;
        }

        Hide();
        _hiddenAt = DateTime.UtcNow;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            _hiddenAt = DateTime.UtcNow;
        }

        base.OnKeyDown(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _hiddenAt = DateTime.UtcNow;
    }

    private void ShowNearCursor()
    {
        _allowDeactivate = false;
        PlaceNearCursor();
        Show();
        Activate();
        PlaceNearCursor();
        _allowDeactivate = true;
    }

    private void PlaceNearCursor()
    {
        UpdateLayout();
        var scale = Scale();
        var cursor = System.Windows.Forms.Cursor.Position;
        var area = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
        var width = ActualWidth > 1 ? ActualWidth : Width;
        var height = ActualHeight > 1 ? ActualHeight : 560;
        var x = cursor.X / scale.X - width + 28;
        var y = cursor.Y / scale.Y - height - 10;
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr handle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr handle, int index, IntPtr value);
}
