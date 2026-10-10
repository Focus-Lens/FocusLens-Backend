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

    public StudyTimeGoalPeriod Period { get; }

    public int TargetMinutes { get; private set; }

    public IReadOnlyCollection<DayOfWeek> Days { get; private set; } = [];

    public DateOnly? StartDate { get; }

    /// <summary>
    ///     The inclusive end of a finite goal period. Weekly goals are deliberately
    ///     tied to the week in which they were created; they do not renew.
    /// </summary>
    public DateOnly? EndDate => Period == StudyTimeGoalPeriod.Weekly && StartDate is DateOnly startDate
        ? startDate.AddDays(6)
        : null;

    public bool IsActiveOn(DateOnly date)
    {
        if (StartDate is not DateOnly startDate || date < startDate)
        {
            return false;
        }

        return EndDate is not DateOnly endDate || date <= endDate;
    }

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

        if (startDate is null)
        {
            return StudyTimeGoalErrors.StartDateRequired;
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

        if (period == StudyTimeGoalPeriod.Weekly && values.Length != 1)
        {
            return StudyTimeGoalErrors.WeeklyStartDayRequired;
        }

        return new StudyTimeGoal(
            period,
            targetMinutes,
            values,
            startDate);
    }
}