using System;
using System.Collections.Generic;

namespace WinMeter.Ui;

internal readonly record struct PlotPoint(double X, double Value);

// Lays samples out on a time axis and thins them to about one point per two pixels.
internal static class SparklinePlot
{
    // capacity: how many samples the full width stands for (0 = exactly the samples given).
    // origin: running sample number of values[0]. Buckets are aligned to it, so they do not
    // shift and flicker as new samples arrive.
    public static List<PlotPoint> Points(IReadOnlyList<double> values, int capacity, long origin, double width)
    {
        var points = new List<PlotPoint>();
        var count = values.Count;
        if (count == 0 || width <= 0)
        {
            return points;
        }

        var slots = Math.Max(capacity, count);
        var span = slots - 1;
        var newest = origin + count - 1;
        var windowStart = newest - span;
        var target = Math.Max(2, (int)(width / 2));
        var bucket = Math.Max(1, (int)Math.Ceiling(slots / (double)target));

        var peak = double.MinValue;
        for (var i = 0; i < count; i++)
        {
            var sequence = origin + i;
            peak = Math.Max(peak, values[i]);
            var closesBucket = i == count - 1 || (sequence + 1) / bucket != sequence / bucket;
            if (!closesBucket)
            {
                continue;
            }

            var x = span == 0 ? width : (sequence - windowStart) * width / span;
            points.Add(new PlotPoint(x, peak));
            peak = double.MinValue;
        }

        return points;
    }
}
