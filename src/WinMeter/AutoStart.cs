using System;
using Microsoft.Win32;

namespace WinMeter;

internal static class AutoStart
{
    private const string ValueName = "WinMeter";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        key.SetValue(ValueName, "\"" + path + "\"");
    }
}
