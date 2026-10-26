using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinMeter.Metrics;

/// <summary>
/// English PDH names, translated for the installed Windows language, then expanded.
/// </summary>
internal static class PdhNames
{
    private const uint FormatDouble = 0x00000200;
    private static readonly Dictionary<string, string> Translated = new(StringComparer.OrdinalIgnoreCase);

    public static string Translate(string english)
    {
        lock (Translated)
        {
            if (Translated.TryGetValue(english, out var cached))
            {
                return cached;
            }

            var local = Lookup(english);
            Translated[english] = local;
            return local;
        }
    }

    public static string[] Expand(string path)
    {
        var chars = 2048;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var buffer = Marshal.AllocHGlobal(chars * 2);
            try
            {
                var size = (uint)chars;
                var status = PdhExpandWildCardPath(null, path, buffer, ref size, 0);
                if (status == 0x800007D2)
                {
                    chars = (int)size + 16;
                    continue;
                }

                if (status != 0 || size < 2)
                {
                    return Array.Empty<string>();
                }

                var copy = (int)Math.Min(size, (uint)chars);
                var text = new char[copy];
                Marshal.Copy(buffer, text, 0, copy);
                return new string(text).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return Array.Empty<string>();
    }

    public static string Instance(string path)
    {
        var open = path.IndexOf('(');
        var close = path.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return path;
        }

        return path.Substring(open + 1, close - open - 1);
    }

    // GPU counters identify an adapter as luid_0xHHHHHHHH_0xHHHHHHHH.
    public static string Luid(string path)
    {
        const string marker = "luid_";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0 || index + 26 > path.Length)
        {
            return "";
        }

        return path.Substring(index, 26).ToLowerInvariant();
    }

    public static double Value(IntPtr counter)
    {
        if (counter == IntPtr.Zero)
        {
            return 0;
        }

        if (PdhGetFormattedCounterValue(counter, FormatDouble, out _, out var raw) != 0
            || double.IsNaN(raw.DoubleValue)
            || double.IsInfinity(raw.DoubleValue)
            || raw.DoubleValue < 0)
        {
            return 0;
        }

        return raw.DoubleValue;
    }

    private static string Lookup(string english)
    {
        try
        {
            using var perflib = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Perflib");
            if (perflib == null)
            {
                return english;
            }

            var language = perflib.GetValue("CurrentLanguage") as string ?? "009";
            using var englishKey = perflib.OpenSubKey("009");
            using var localKey = perflib.OpenSubKey(language);
            var englishMap = Names(englishKey);
            var localMap = Names(localKey);
            foreach (var pair in englishMap)
            {
                if (!string.Equals(pair.Value, english, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return localMap.TryGetValue(pair.Key, out var local) && local.Length > 0 ? local : english;
            }
        }
        catch (Exception)
        {
        }

        return english;
    }

    private static Dictionary<int, string> Names(RegistryKey? key)
    {
        var map = new Dictionary<int, string>();
        if (key?.GetValue("Counter") is not string[] rows)
        {
            return map;
        }

        for (var i = 0; i + 1 < rows.Length; i += 2)
        {
            if (int.TryParse(rows[i], out var index))
            {
                map[index] = rows[i + 1];
            }
        }

        return map;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhExpandWildCardPath(string? dataSource, string wildCardPath, IntPtr expanded, ref uint pathListLength, uint flags);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhFormattedValue value);

    [StructLayout(LayoutKind.Explicit)]
    private struct PdhFormattedValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double DoubleValue;
    }
}
