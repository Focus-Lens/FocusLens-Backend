namespace FocusLens.Contracts.Home;

public sealed record StudyOverviewResponse(
    int StreakDays,
    int SessionsToday,
    int? FocusTimeMinutes,
    int? WeeklyGoalMinutes);