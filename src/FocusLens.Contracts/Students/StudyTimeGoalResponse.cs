namespace FocusLens.Contracts.Students;

/// <param name="Days">
/// Selected weekdays for daily goals. For weekly goals, this contains exactly one value: the day the week starts.
/// </param>
public sealed record StudyTimeGoalResponse(
    string Period,
    int TargetMinutes,
    IReadOnlyCollection<DayOfWeek> Days,
    DateOnly? StartDate
);