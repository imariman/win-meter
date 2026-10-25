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

        var line = new StreamGeometry();
        var fill = new StreamGeometry();
        using (var lineContext = line.Open())
        using (var fillContext = fill.Open())
        {
            var started = false;
            var first = new Point();
            var last = new Point();
            for (var i = 0; i < values.Count; i++)
            {
                var x = values.Count == 1 ? 0 : i * (width / (values.Count - 1));
                var clamped = Math.Clamp(values[i], 0, max);
                var y = height - 1 - (clamped / max) * (height - 2);
                var point = new Point(x, y);
                if (!started)
                {
                    first = point;
                    last = point;
                    lineContext.BeginFigure(point, false, false);
                    fillContext.BeginFigure(new Point(point.X, height - 1), true, true);
                    fillContext.LineTo(point, true, false);
                    started = true;
                }
                else
                {
                    lineContext.LineTo(point, true, true);
                    fillContext.LineTo(point, true, false);
                    last = point;
                }
            }

            if (values.Count == 1)
            {
                var end = new Point(width, first.Y);
                lineContext.LineTo(end, true, true);
                fillContext.LineTo(end, true, false);
                last = end;
            }

            fillContext.LineTo(new Point(last.X, height - 1), true, false);
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

        var lastValue = Math.Clamp(values[values.Count - 1], 0, max);
        var marker = new Point(width, height - 1 - (lastValue / max) * (height - 2));
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
