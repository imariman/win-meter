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

        context.DrawLine(
            new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1),
            new Point(0, height - 1),
            new Point(width, height - 1));

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
        var fillBrush = Stroke.Clone();
        fillBrush.Opacity = 0.18;
        context.DrawGeometry(fillBrush, null, fill);
        context.DrawGeometry(null, new Pen(Stroke, 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        }, line);
    }
}
