using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WinMeter.Ui;

public class SparklineControl : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IReadOnlyList<double>),
        typeof(SparklineControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(SparklineControl),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RangeMaxProperty = DependencyProperty.Register(
        nameof(RangeMax),
        typeof(double),
        typeof(SparklineControl),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity),
        typeof(int),
        typeof(SparklineControl),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OriginProperty = DependencyProperty.Register(
        nameof(Origin),
        typeof(long),
        typeof(SparklineControl),
        new FrameworkPropertyMetadata(0L, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double RangeMax
    {
        get => (double)GetValue(RangeMaxProperty);
        set => SetValue(RangeMaxProperty, value);
    }

    // Number of samples the full width stands for. Fewer samples are drawn against the right edge.
    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    // Running sample number of Values[0]; keeps thinned points steady while the chart scrolls.
    public long Origin
    {
        get => (long)GetValue(OriginProperty);
        set => SetValue(OriginProperty, value);
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        context.DrawLine(GridPen, new Point(0, height - 1), new Point(width, height - 1));
        context.DrawLine(GuidePen, new Point(0, Math.Round(height / 2) + 0.5), new Point(width, Math.Round(height / 2) + 0.5));

        var values = Values;
        if (values == null || values.Count == 0)
        {
            return;
        }

        var max = RangeMax > 0 ? RangeMax : 1;
        if (RangeMax <= 0)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                }
            }
        }

        var points = SparklinePlot.Points(values, Capacity, Origin, width);
        var plotted = new Point[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var clamped = Math.Clamp(points[i].Value, 0, max);
            plotted[i] = new Point(points[i].X, height - 1 - (clamped / max) * (height - 2));
        }

        if (plotted.Length > 1)
        {
            var line = new StreamGeometry();
            var fill = new StreamGeometry();
            using (var lineContext = line.Open())
            using (var fillContext = fill.Open())
            {
                lineContext.BeginFigure(plotted[0], false, false);
                fillContext.BeginFigure(new Point(plotted[0].X, height - 1), true, true);
                fillContext.LineTo(plotted[0], true, false);
                for (var i = 1; i < plotted.Length; i++)
                {
                    lineContext.LineTo(plotted[i], true, true);
                    fillContext.LineTo(plotted[i], true, false);
                }

                fillContext.LineTo(new Point(plotted[^1].X, height - 1), true, false);
            }

            line.Freeze();
            fill.Freeze();
            context.DrawGeometry(FillBrush(), null, fill);
            context.DrawGeometry(null, new Pen(Stroke, 1.5)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            }, line);
        }

        var marker = plotted[^1];
        var halo = Stroke.Clone();
        halo.Opacity = 0.25;
        context.DrawEllipse(halo, null, marker, 5, 5);
        context.DrawEllipse(Stroke, null, marker, 2.5, 2.5);
    }

    private static readonly Pen GridPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1));

    private static readonly Pen GuidePen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), 1)
    {
        DashStyle = new DashStyle(new[] { 2d, 3d }, 0)
    });

    // Fades the stroke color from the line down to nothing at the baseline.
    private Brush FillBrush()
    {
        if (Stroke is not SolidColorBrush solid)
        {
            var flat = Stroke.Clone();
            flat.Opacity = 0.18;
            return flat;
        }

        var color = solid.Color;
        var brush = new LinearGradientBrush(
            Color.FromArgb(80, color.R, color.G, color.B),
            Color.FromArgb(0, color.R, color.G, color.B),
            90);
        brush.Freeze();
        return brush;
    }

    private static Pen Frozen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }
}
