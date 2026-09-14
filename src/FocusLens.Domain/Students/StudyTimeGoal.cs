using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.Students;

public sealed record StudyTimeGoal
{
    private StudyTimeGoal()
    {
    }

    private StudyTimeGoal(
        StudyTimeGoalPeriod period,
        int targetMinutes,
        IReadOnlyCollection<DayOfWeek> days,
        DateOnly? startDate)
    {
        Period = period;
        TargetMinutes = targetMinutes;
        Days = days.ToArray();
        StartDate = startDate;
    }

    public StudyTimeGoalPeriod Period { get; private set; }

    public int TargetMinutes { get; private set; }

    public IReadOnlyCollection<DayOfWeek> Days { get; private set; } = [];

    public DateOnly? StartDate { get; private set; }

    public static Result<StudyTimeGoal> Create(
        StudyTimeGoalPeriod period,
        int targetMinutes,
        IEnumerable<DayOfWeek> days,
        DateOnly? startDate)
    {
        if (!Enum.IsDefined(period))
        {
            return StudyTimeGoalErrors.InvalidPeriod;
        }

        if (targetMinutes <= 0)
        {
            return StudyTimeGoalErrors.TargetMinutesInvalid;
        }

        ArgumentNullException.ThrowIfNull(days);

        DayOfWeek[] values = days.ToArray();

        if (values.Distinct().Count() != values.Length)
        {
            return StudyTimeGoalErrors.DuplicateDays;
        }

        if (values.Any(day => !Enum.IsDefined(day)))
        {
            return StudyTimeGoalErrors.InvalidDay;
        }

        if (period == StudyTimeGoalPeriod.Daily && values.Length == 0)
        {
            return StudyTimeGoalErrors.DaysRequired;
        }

        if (period == StudyTimeGoalPeriod.Weekly && values.Length > 0)
        {
            return StudyTimeGoalErrors.DaysNotAllowed;
        }

        return new StudyTimeGoal(
            period,
            targetMinutes,
            values,
            startDate);
    }
}