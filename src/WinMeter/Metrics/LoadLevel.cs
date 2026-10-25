namespace WinMeter.Metrics;

internal static class LoadLevel
{
    public const string Normal = "";
    public const string High = "High";
    public const string Critical = "Critical";

    public static string For(double percent)
    {
        if (percent >= 90)
        {
            return Critical;
        }

        if (percent >= 75)
        {
            return High;
        }

        return Normal;
    }
}
