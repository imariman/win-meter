using System;
using System.Runtime.InteropServices;

namespace WinMeter.Metrics;

internal readonly record struct BatteryReading(bool Present, double Percent, bool Charging, bool OnAc, int SecondsRemaining)
{
    public static BatteryReading None => new(false, 0, false, true, -1);
}

internal static class BatteryReader
{
    public static BatteryReading Read()
    {
        try
        {
            if (!GetSystemPowerStatus(out var status))
            {
                return BatteryReading.None;
            }

            if ((status.BatteryFlag & 128) != 0 || status.BatteryLifePercent == 255)
            {
                return BatteryReading.None;
            }

            var seconds = status.BatteryLifeTime;
            return new BatteryReading(
                true,
                status.BatteryLifePercent,
                (status.BatteryFlag & 8) != 0,
                status.AcLineStatus == 1,
                seconds < 0 ? -1 : seconds);
        }
        catch (Exception)
        {
            return BatteryReading.None;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
