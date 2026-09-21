namespace FocusLens.Contracts.Students;

/// <param name="Days">
/// Selected weekdays for daily goals. For weekly goals, this must contain exactly one value: the day the week starts.
/// </param>
public sealed record StudyTimeGoalRequest(
    StudyTimeGoalPeriod Period,
    int TargetMinutes,
    IReadOnlyCollection<DayOfWeek>? Days,
    DateOnly? StartDate = null
);