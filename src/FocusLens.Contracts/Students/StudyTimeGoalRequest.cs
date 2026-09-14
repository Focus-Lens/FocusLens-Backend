namespace FocusLens.Contracts.Students;

public sealed record StudyTimeGoalRequest(
    StudyTimeGoalPeriod Period,
    int TargetMinutes,
    IReadOnlyCollection<DayOfWeek>? Days,
    DateOnly? StartDate
);