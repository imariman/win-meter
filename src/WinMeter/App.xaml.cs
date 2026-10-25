using System;
using System.IO;
using System.Threading;
using System.Windows;
using WinMeter.Metrics;
using WinMeter.Tray;

namespace WinMeter;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private SystemSampler? _sampler;
    private TrayController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (IsOnce(e.Args))
        {
            Shutdown(SampleOnce());
            return;
        }

        try
        {
            _instanceMutex = new Mutex(true, @"Local\WinMeter.SingleInstance", out var created);
            _ownsMutex = created;
            if (!created)
            {
                Shutdown();
                return;
            }
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        _sampler = new SystemSampler();
        _tray = new TrayController(_sampler);
        _tray.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _sampler?.Dispose();
        if (_ownsMutex)
        {
            try
            {
                _instanceMutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static bool IsOnce(string[] args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--once", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int SampleOnce()
    {
        using var sampler = new SystemSampler();
        sampler.Start();
        var deadline = DateTime.UtcNow.AddSeconds(4);
        Snapshot? sample = null;
        while (DateTime.UtcNow < deadline)
        {
            sample = sampler.Latest;
            if (sample is { HasDelta: true, RamTotalBytes: > 0 })
            {
                break;
            }

            Thread.Sleep(150);
        }

        if (sample == null || sample.RamTotalBytes <= 0)
        {
            WriteLine("winmeter: ölçüm alınamadı");
            return 1;
        }

        WriteLine(
            "cpu=" + sample.CpuPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " ram=" + sample.RamPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " disk=" + sample.DiskUsedPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " read=" + sample.DiskReadBps.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + " write=" + sample.DiskWriteBps.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + " down=" + sample.NetDownBps.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + " up=" + sample.NetUpBps.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + " iface=" + sample.NetName
            + " volume=" + sample.DiskName);
        return 0;
    }

    private static void WriteLine(string text)
    {
        var handle = GetStdHandle(-11);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "winmeter-once.txt"), text + Environment.NewLine);
            return;
        }

        using var stream = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(handle, ownsHandle: false), FileAccess.Write);
        using var writer = new StreamWriter(stream) { AutoFlush = true };
        writer.WriteLine(text);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handle);
}
