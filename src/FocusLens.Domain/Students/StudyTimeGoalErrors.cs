using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.Students;

public static class StudyTimeGoalErrors
{
    public static readonly Error InvalidPeriod =
        Error.Validation(
            "StudyTimeGoals.InvalidPeriod",
            "Study-time goal period is invalid.");

    public static readonly Error TargetMinutesInvalid =
        Error.Validation(
            "StudyTimeGoals.TargetMinutesInvalid",
            "Study-time goal must be greater than zero.");

    public static readonly Error DaysRequired =
        Error.Validation(
            "StudyTimeGoals.DaysRequired",
            "A daily study-time goal must contain at least one day.");

    public static readonly Error WeeklyStartDayRequired =
        Error.Validation(
            "StudyTimeGoals.WeeklyStartDayRequired",
            "A weekly study-time goal must contain exactly one week start day.");

    public static readonly Error StartDateRequired =
        Error.Validation(
            "StudyTimeGoals.StartDateRequired",
            "Study-time goal start date is required.");

    public static readonly Error DuplicateDays =
        Error.Validation(
            "StudyTimeGoals.DuplicateDays",
            "Study-time goal days must not contain duplicates.");

    public static readonly Error InvalidDay =
        Error.Validation(
            "StudyTimeGoals.InvalidDay",
            "Study-time goal contains an invalid day.");
}