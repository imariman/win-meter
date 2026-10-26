using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WinMeter.Metrics;

/// <summary>
/// ACPI thermal zones, plus NVIDIA GPU temperature when nvml.dll is already installed.
/// No kernel driver is loaded.
/// </summary>
internal sealed class ThermalReader : IDisposable
{
    private readonly PdhWildcard _precise = new("Thermal Zone Information", "High Precision Temperature");
    private readonly PdhWildcard _coarse = new("Thermal Zone Information", "Temperature");
    private readonly NvmlTemperatures _nvml = new();
    private ThermalZone[] _last = Array.Empty<ThermalZone>();
    private DateTime _lastAt = DateTime.MinValue;

    public void Collect()
    {
        _precise.Collect();
        _coarse.Collect();
    }

    public ThermalZone[] Read()
    {
        try
        {
            Collect();
            var fresh = ReadFresh();
            if (fresh.Length > 0)
            {
                _last = fresh;
                _lastAt = DateTime.UtcNow;
                return fresh;
            }
        }
        catch (Exception)
        {
        }

        return _last.Length > 0 && DateTime.UtcNow - _lastAt < TimeSpan.FromSeconds(15)
            ? _last
            : Array.Empty<ThermalZone>();
    }

    public void Dispose()
    {
        _precise.Dispose();
        _coarse.Dispose();
        _nvml.Dispose();
    }

    private ThermalZone[] ReadFresh()
    {
        var precise = _precise.Read();
        var source = precise.Length > 0 ? precise : _coarse.Read();
        var tenthsOfKelvin = precise.Length > 0;
        var zones = new List<ThermalZone>();
        foreach (var item in source)
        {
            var celsius = ToCelsius(item.Value, tenthsOfKelvin);
            if (celsius < 1 || celsius > 125)
            {
                continue;
            }

            zones.Add(new ThermalZone(Clean(item.Instance), celsius));
        }

        foreach (var gpu in _nvml.Read())
        {
            zones.Add(gpu);
        }

        return zones.ToArray();
    }

    // High Precision Temperature is tenths of a Kelvin. Temperature is whole Kelvin.
    // A value that is already Celsius (below 200) is kept as-is.
    private static double ToCelsius(double raw, bool tenthsOfKelvin)
    {
        var value = tenthsOfKelvin ? raw / 10.0 : raw;
        if (value > 200)
        {
            value -= 273.15;
        }

        return value;
    }

    private static string Clean(string instance)
    {
        var name = instance.Trim();
        var slash = name.LastIndexOf('\\');
        if (slash >= 0 && slash < name.Length - 1)
        {
            name = name.Substring(slash + 1);
        }

        if (name.StartsWith("_TZ.", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(4);
        }

        name = name.Trim();
        if (name.Length == 0)
        {
            return "Zone";
        }

        if (name.StartsWith("tz", StringComparison.OrdinalIgnoreCase) && name.Length <= 6)
        {
            return name.ToUpperInvariant();
        }

        return name;
    }

    private sealed class NvmlTemperatures : IDisposable
    {
        private int _state;
        private bool _ready;

        public ThermalZone[] Read()
        {
            if (_state < 0 || !Ensure())
            {
                return Array.Empty<ThermalZone>();
            }

            try
            {
                if (nvmlDeviceGetCount_v2(out var count) != 0 || count == 0 || count > 16)
                {
                    return Array.Empty<ThermalZone>();
                }

                var zones = new List<ThermalZone>((int)count);
                var name = new byte[96];
                for (uint index = 0; index < count; index++)
                {
                    if (nvmlDeviceGetHandleByIndex_v2(index, out var device) != 0)
                    {
                        continue;
                    }

                    if (nvmlDeviceGetTemperature(device, 0, out var temp) != 0 || temp is < 1 or > 125)
                    {
                        continue;
                    }

                    var label = "GPU";
                    if (nvmlDeviceGetName(device, name, (uint)name.Length) == 0)
                    {
                        var end = Array.IndexOf(name, (byte)0);
                        var text = Encoding.UTF8.GetString(name, 0, end < 0 ? name.Length : end).Trim();
                        if (text.Length > 0)
                        {
                            label = text;
                        }
                    }

                    zones.Add(new ThermalZone(label, temp));
                }

                return zones.ToArray();
            }
            catch (Exception)
            {
                _state = -1;
                return Array.Empty<ThermalZone>();
            }
        }

        public void Dispose()
        {
            if (_ready)
            {
                try
                {
                    nvmlShutdown();
                }
                catch (Exception)
                {
                }

                _ready = false;
            }
        }

        private bool Ensure()
        {
            if (_state != 0)
            {
                return _state > 0;
            }

            var paths = new[]
            {
                "nvml.dll",
                @"C:\Windows\System32\nvml.dll",
                @"C:\Program Files\NVIDIA Corporation\NVSMI\nvml.dll"
            };
            foreach (var path in paths)
            {
                try
                {
                    if (path != "nvml.dll" && !System.IO.File.Exists(path))
                    {
                        continue;
                    }

                    if (LoadLibrary(path) == IntPtr.Zero)
                    {
                        continue;
                    }

                    var status = nvmlInit_v2();
                    if (status == 0)
                    {
                        _ready = true;
                        _state = 1;
                        return true;
                    }
                }
                catch (Exception)
                {
                }
            }

            _state = -1;
            return false;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string path);

        [DllImport("nvml.dll")]
        private static extern int nvmlInit_v2();

        [DllImport("nvml.dll")]
        private static extern int nvmlShutdown();

        [DllImport("nvml.dll")]
        private static extern int nvmlDeviceGetCount_v2(out uint count);

        [DllImport("nvml.dll")]
        private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

        [DllImport("nvml.dll")]
        private static extern int nvmlDeviceGetName(IntPtr device, byte[] name, uint length);

        [DllImport("nvml.dll")]
        private static extern int nvmlDeviceGetTemperature(IntPtr device, int sensor, out uint temp);
    }
}
