using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WinMeter.Metrics;

namespace WinMeter.Ui;

// Maps a LoadLevel to its warning brush, or to the brush passed as the parameter when the load is normal.
public sealed class LevelBrushConverter : IValueConverter
{
    public Brush High { get; set; } = Brushes.Orange;
    public Brush Critical { get; set; } = Brushes.Red;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value as string) switch
        {
            LoadLevel.Critical => Critical,
            LoadLevel.High => High,
            _ => parameter as Brush ?? Brushes.White
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class LevelAccentConverter : IMultiValueConverter
{
    public Brush High { get; set; } = Brushes.Orange;
    public Brush Critical { get; set; } = Brushes.Red;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var level = values.Length > 0 ? values[0] as string : null;
        var accent = values.Length > 1 ? values[1] as Brush : null;
        return level switch
        {
            LoadLevel.Critical => Critical,
            LoadLevel.High => High,
            _ => accent ?? Brushes.White
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
