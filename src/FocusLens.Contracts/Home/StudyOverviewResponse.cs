namespace FocusLens.Contracts.Home;

public sealed record StudyOverviewResponse(
    int StreakDays,
    int SessionsToday,
    int? FocusTimeMinutes,
    string? WeeklyGoalTime,
    string WeeklyStudiedTime,
    decimal WeeklyGoalPercentage,
    bool HasStartedSession);
