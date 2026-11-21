using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;

namespace FocusLens.Application.Parents;

/// <summary>Single calendar boundary for all parent overview reads and updates.</summary>
internal static class OverviewWeekResolver
{
    public static async Task<Result<ParentOverviewWeekResponse>> ResolveAsync(
        ParentDashboardContext context, IStudentLocalTime studentLocalTime,
        IBaseRepository<StudentWeek> weekRepository, IUnitOfWork? unitOfWork = null)
    {
        Result<DayOfWeek> weekStartsOnResult = GetParentWeekStartsOn(context);
        if (weekStartsOnResult.IsError)
        {
            return weekStartsOnResult.Errors;
        }

        DateOnly? persistedStart = context.Relationship.SelectedOverviewWeekStart;
        DateOnly? selectedStart = persistedStart is null
            ? null
            : ParentWeekdayOrder.GetWeekStart(persistedStart.Value, weekStartsOnResult.Value);
        if (selectedStart != persistedStart)
        {
            // A selected date identifies a calendar period. When the parent changes
            // their week-start preference, retain that period but rebase its start.
            context.Relationship.SetSelectedOverviewWeekStart(selectedStart);
        }

        Result<StudentWeek> actualCurrentWeek = await ResolveActualCurrentWeekAsync(
            context, studentLocalTime, weekRepository, unitOfWork);
        if (actualCurrentWeek.IsError)
        {
            return actualCurrentWeek.Errors;
        }

        DateOnly currentStart = selectedStart ?? actualCurrentWeek.Value.StartsOn;
        return new ParentOverviewWeekResponse(
            currentStart,
            currentStart.AddDays(6),
            currentStart == actualCurrentWeek.Value.StartsOn);
    }

    public static async Task<Result<StudentWeek>> ResolveActualCurrentWeekAsync(
        ParentDashboardContext context, IStudentLocalTime studentLocalTime,
        IBaseRepository<StudentWeek> weekRepository, IUnitOfWork? unitOfWork = null)
    {
        Result<DayOfWeek> weekStartsOnResult = GetParentWeekStartsOn(context);
        if (weekStartsOnResult.IsError)
        {
            return weekStartsOnResult.Errors;
        }

        // Parent dashboard calendar boundaries are owned by the parent. The student's
        // local date is retained only to make "today" correct for that student.
        DateOnly currentStart =
            ParentWeekdayOrder.GetWeekStart(studentLocalTime.GetToday(context.Student), weekStartsOnResult.Value);
        return await GetOrCreateWeekAsync(context, currentStart, weekRepository, unitOfWork);
    }

    public static Result<DayOfWeek> GetParentWeekStartsOn(ParentDashboardContext context)
    {
        if (context.Parent.WeekStartsOn is not DayOfWeek weekStartsOn)
        {
            return Error.Validation("ParentOverviewWeek.ParentWeekStartsOnRequired",
                "The parent must have a week start day configured.");
        }

        return weekStartsOn;
    }

    public static DateOnly NormalizeToParentWeekStart(DateOnly date, DayOfWeek weekStartsOn) =>
        ParentWeekdayOrder.GetWeekStart(date, weekStartsOn);

    private static async Task<StudentWeek> GetOrCreateWeekAsync(
        ParentDashboardContext context,
        DateOnly startsOn,
        IBaseRepository<StudentWeek> weekRepository,
        IUnitOfWork? unitOfWork)
    {
        StudentWeek? current = await weekRepository.FirstOrDefaultAsync(week =>
            week.StudentId == context.Student.Id && week.StartsOn == startsOn);
        if (current is null)
        {
            current = new StudentWeek(context.Student.Id, startsOn);
            weekRepository.Add(current);
            if (unitOfWork is not null)
            {
                await unitOfWork.SaveChangesAsync();
            }
        }

        return current;
    }
}
