namespace FocusLens.Contracts.Students;

public sealed record StudyTimeGoalResponse(
    string Period,
    int TargetMinutes,
    IReadOnlyCollection<DayOfWeek> Days,
    DateOnly? StartDate
);