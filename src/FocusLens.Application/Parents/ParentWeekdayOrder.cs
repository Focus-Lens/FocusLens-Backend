using FocusLens.Application.Common.Utilities;

namespace FocusLens.Application.Parents;

internal static class ParentWeekdayOrder
{
    public static IReadOnlyCollection<DayOfWeek> OrderDays(
        IEnumerable<DayOfWeek> days,
        DayOfWeek weekStartsOn)
    {
        return days
            .OrderBy(day => ((int)day - (int)weekStartsOn + 7) % 7)
            .ToArray();
    }

    public static DateOnly GetWeekStart(DateOnly date, DayOfWeek weekStartsOn)
    {
        return WeekStartCalculator.GetWeekStart(date, weekStartsOn);
    }

}
