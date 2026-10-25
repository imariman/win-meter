using System;
using Microsoft.Win32;

namespace WinMeter.Ui;

internal static class StripPreferences
{
    private const string KeyPath = @"Software\WinMeter";

    public static event EventHandler? Changed;

    public static bool Shows(MeterKind kind)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        if (key?.GetValue(kind.ToString()) is int number)
        {
            return number != 0;
        }

        return kind is MeterKind.Cpu or MeterKind.Memory;
    }

    public static void Set(MeterKind kind, bool shown)
    {
        if (Shows(kind) == shown)
        {
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(kind.ToString(), shown ? 1 : 0, RegistryValueKind.DWord);

        Changed?.Invoke(null, EventArgs.Empty);
    }
}
