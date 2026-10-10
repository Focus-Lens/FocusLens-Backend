namespace FocusLens.Application.Common.Utilities;

public static class DurationDisplayFormatter
{
    public static string FormatMinutes(int minutes)
    {
        long absoluteMinutes = Math.Abs((long)minutes);
        long hours = absoluteMinutes / 60;
        long remainder = absoluteMinutes % 60;
        string prefix = minutes < 0 ? "-" : string.Empty;
        if (hours == 0)
        {
            return $"{prefix}{remainder} {Unit(remainder, "minute")}";
        }

        if (remainder == 0)
        {
            return $"{prefix}{hours} {Unit(hours, "hour")}";
        }

        return $"{prefix}{hours} {Unit(hours, "hour")} {remainder} {Unit(remainder, "minute")}";
    }

    private static string Unit(long value, string unit) => value == 1 ? unit : $"{unit}s";
}