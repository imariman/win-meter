using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinMeter.Tray;

/// <summary>
/// Windows 11 keeps a new notification icon in the overflow arrow until its
/// NotifyIconSettings value IsPromoted is 1. That is the switch for the slot
/// beside the clock.
/// </summary>
internal static class TrayPromoter
{
    private const int SettingChange = 0x001A;
    private const uint AbortIfHung = 0x0002;

    public static void PromoteCurrentProcess()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            using var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", writable: true);
            if (root == null)
            {
                return;
            }

            var changed = false;
            foreach (var name in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(name, writable: true);
                if (key == null)
                {
                    continue;
                }

                var executable = key.GetValue("ExecutablePath") as string;
                if (!string.Equals(executable, path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (key.GetValue("IsPromoted") is int promoted && promoted == 1)
                {
                    continue;
                }

                key.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                changed = true;
            }

            if (changed)
            {
                SendMessageTimeout(
                    new IntPtr(0xffff),
                    SettingChange,
                    IntPtr.Zero,
                    "TraySettings",
                    AbortIfHung,
                    1000,
                    out _);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        int message,
        IntPtr wParam,
        string lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
