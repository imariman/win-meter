using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace WinMeter.Tray;

internal static class IconFactory
{
    public static Icon Create(string label, string value, Color color)
    {
        var size = SmallIconSize();
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);
            using var background = Rounded(new Rectangle(0, 0, size - 1, size - 1), Math.Max(3, size / 6));
            using var fill = new SolidBrush(Color.FromArgb(235, 18, 20, 28));
            graphics.FillPath(fill, background);

            using var brush = new SolidBrush(color);
            if (size >= 24)
            {
                using var labelFont = new Font("Segoe UI", Math.Max(7f, size * 0.22f), FontStyle.Bold, GraphicsUnit.Pixel);
                using var valueFont = new Font("Segoe UI", Math.Max(10f, size * 0.34f), FontStyle.Bold, GraphicsUnit.Pixel);
                DrawCentered(graphics, label, labelFont, brush, size, size * 0.06f);
                DrawCentered(graphics, value, valueFont, brush, size, size * 0.42f);
            }
            else
            {
                using var valueFont = new Font("Segoe UI", Math.Max(8f, size * 0.46f), FontStyle.Bold, GraphicsUnit.Pixel);
                var measured = graphics.MeasureString(value, valueFont);
                graphics.DrawString(value, valueFont, brush, (size - measured.Width) / 2f, (size - measured.Height) / 2f);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public static Color ColorForPercent(double percent)
    {
        if (percent >= 90)
        {
            return Color.FromArgb(255, 112, 112);
        }

        if (percent >= 75)
        {
            return Color.FromArgb(245, 186, 78);
        }

        return Color.FromArgb(236, 239, 244);
    }

    private static void DrawCentered(Graphics graphics, string text, Font font, Brush brush, int size, float top)
    {
        var measured = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, brush, (size - measured.Width) / 2f, top);
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static int SmallIconSize()
    {
        var size = GetSystemMetrics(49);
        if (size < 16)
        {
            size = 16;
        }

        if (size > 64)
        {
            size = 64;
        }

        return size;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
