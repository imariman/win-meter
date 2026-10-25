using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinMeter.Metrics;

internal sealed record MachineProfile(string Model, string Processor, string Graphics, string Windows)
{
    public static MachineProfile Read()
    {
        var maker = Clean(RegistryText(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer"));
        var product = Clean(RegistryText(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName"));
        if (IsGeneric(product))
        {
            product = Clean(RegistryText(@"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardProduct"));
        }

        var model = "This PC";
        if (!IsGeneric(product) && !IsGeneric(maker) && !product.StartsWith(maker, StringComparison.OrdinalIgnoreCase))
        {
            model = maker + " " + product;
        }
        else if (!IsGeneric(product))
        {
            model = product;
        }
        else if (!IsGeneric(maker))
        {
            model = maker;
        }

        var processor = Clean(RegistryText(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString"));
        if (processor.Length == 0)
        {
            processor = "Processor";
        }

        var graphics = string.Join(" · ", DisplayAdapters());
        if (graphics.Length == 0)
        {
            graphics = "Graphics";
        }

        return new MachineProfile(model, processor, graphics, WindowsLabel());
    }

    private static string WindowsLabel()
    {
        var name = RegistryText(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName");
        var display = RegistryText(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
        var buildText = RegistryText(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber");
        if (int.TryParse(buildText, out var build) && build >= 22000 && name.StartsWith("Windows 10", StringComparison.Ordinal))
        {
            name = "Windows 11" + name.Substring("Windows 10".Length);
        }

        name = Clean(name);
        if (name.Length == 0)
        {
            name = "Windows";
        }

        display = Clean(display);
        return display.Length == 0 ? name : name + " " + display;
    }

    private static List<string> DisplayAdapters()
    {
        var names = new List<string>();
        var fallback = new List<string>();
        for (uint index = 0; index < 16; index++)
        {
            var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, index, ref device, 0))
            {
                break;
            }

            var name = Clean(device.DeviceString);
            if (name.Length == 0 || names.Contains(name) || fallback.Contains(name))
            {
                continue;
            }

            if (name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Basic Render", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Indirect", StringComparison.OrdinalIgnoreCase))
            {
                fallback.Add(name);
            }
            else
            {
                names.Add(name);
            }
        }

        return names.Count > 0 ? names : fallback;
    }

    private static string RegistryText(string path, string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key?.GetValue(name) as string ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string Clean(string value)
    {
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts).Trim();
    }

    private static bool IsGeneric(string value)
    {
        return value.Length == 0
            || value.Contains("To Be Filled", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Default string", StringComparison.OrdinalIgnoreCase)
            || value.Contains("System Product Name", StringComparison.OrdinalIgnoreCase)
            || value.Contains("O.E.M.", StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice display, uint flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public uint StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }
}
