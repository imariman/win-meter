using System;
using System.Globalization;

namespace WinMeter;

internal static class Format
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Percent(double value)
    {
        return Math.Clamp(value, 0, 100).ToString("0", CultureInfo.CurrentCulture) + "%";
    }

    public static string Bytes(long bytes)
    {
        double value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var number = unit == 0
            ? value.ToString("0", CultureInfo.CurrentCulture)
            : value.ToString("0.0", CultureInfo.CurrentCulture);
        return number + " " + Units[unit];
    }

    public static string Rate(double bytesPerSecond)
    {
        return Bytes((long)Math.Max(0, bytesPerSecond)) + "/s";
    }

    public static string CompactRate(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
        {
            return "0";
        }

        if (bytesPerSecond < 1024 * 1024)
        {
            return (bytesPerSecond / 1024).ToString("0", CultureInfo.InvariantCulture) + "K";
        }

        if (bytesPerSecond < 1024d * 1024 * 1024)
        {
            return (bytesPerSecond / (1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture) + "M";
        }

        return (bytesPerSecond / (1024d * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture) + "G";
    }
}
