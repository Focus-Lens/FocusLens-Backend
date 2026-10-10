namespace FocusLens.Application.Common.Utilities;

public static class WeekStartCalculator
{
    public static DateOnly GetWeekStart(DateOnly date, DayOfWeek weekStartsOn)
    {
        if (!Enum.IsDefined(weekStartsOn))
        {
            throw new ArgumentOutOfRangeException(nameof(weekStartsOn), weekStartsOn, "Week start day is invalid.");
        }

        int daysSinceWeekStarted = ((int)date.DayOfWeek - (int)weekStartsOn + 7) % 7;
        return date.AddDays(-daysSinceWeekStarted);
    }
}