using System;
using WinMeter.Metrics;
using WinMeter.Ui;

namespace WinMeter.Tray;

internal sealed class TrayController : IDisposable
{
    private readonly SystemSampler _sampler;
    private readonly DashboardViewModel _model = new();
    private readonly DashboardWindow _dashboard = new();
    private readonly TaskbarStrip _strip = new();
    private bool _disposed;

    public TrayController(SystemSampler sampler)
    {
        _sampler = sampler;
        _dashboard.DataContext = _model;

        _strip.OpenRequested += (_, _) =>
        {
            _strip.Highlight(null);
            _dashboard.ShowFromMenu();
        };
        _strip.MeterRequested += (_, kind) => _strip.Highlight(_dashboard.ShowMeter(kind) ? kind : null);
        _strip.MenuRequested += (_, _) =>
        {
            _strip.Highlight(null);
            _dashboard.ShowFromMenu();
        };
        _dashboard.Dismissed += (_, _) => _strip.Highlight(null);
        StripPreferences.Changed += OnStripChanged;
    }

    public void Start()
    {
        _strip.ApplyVisibility();
        _sampler.Updated += OnSample;
        _sampler.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sampler.Updated -= OnSample;
        StripPreferences.Changed -= OnStripChanged;
        _strip.Close();
        _dashboard.Close();
    }

    private void OnStripChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_disposed)
            {
                _strip.ApplyVisibility();
            }
        }));
    }

    private void OnSample(object? sender, Snapshot snapshot)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.BeginInvoke(new Action(() =>
        {
            if (_disposed)
            {
                return;
            }

            _model.Apply(snapshot, _sampler.CopyHistory());
            _strip.Apply(snapshot);
        }));
    }
}
