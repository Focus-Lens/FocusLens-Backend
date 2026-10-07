using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.Common.Interfaces;

namespace FocusLens.Application.Parents;

/// <summary>Single calendar boundary for all parent overview reads and updates.</summary>
internal static class OverviewWeekResolver
{
    public static async Task<Result<ParentOverviewWeekResponse>> ResolveAsync(
        ParentDashboardContext context, IStudentLocalTime studentLocalTime,
        IBaseRepository<StudentWeek> weekRepository, IUnitOfWork? unitOfWork = null)
    {
        if (context.Student.WeekStartsOn is not DayOfWeek)
            return Error.Validation("ParentOverviewWeek.StudentWeekStartsOnRequired", "The student must have a week start day configured.");

        DateOnly? persistedStart = context.Relationship.SelectedOverviewWeekStart;
        StudentWeek? selected = persistedStart is null ? null : await weekRepository.FirstOrDefaultAsync(
            week => week.StudentId == context.Student.Id && week.StartsOn == persistedStart.Value);
        if (persistedStart is not null && selected is null)
            return Error.Validation("ParentOverviewWeek.NotFound", "The selected overview week no longer exists.");

        Result<StudentWeek> actualCurrentWeek = await ResolveActualCurrentWeekAsync(
            context, studentLocalTime, weekRepository, unitOfWork);
        if (actualCurrentWeek.IsError) return actualCurrentWeek.Errors;

        StudentWeek current = selected ?? actualCurrentWeek.Value;
        return new ParentOverviewWeekResponse(
            current.StartsOn,
            current.EndsOn,
            current.StartsOn == actualCurrentWeek.Value.StartsOn && current.EndsOn == actualCurrentWeek.Value.EndsOn);
    }

    public static async Task<Result<StudentWeek>> ResolveActualCurrentWeekAsync(
        ParentDashboardContext context, IStudentLocalTime studentLocalTime,
        IBaseRepository<StudentWeek> weekRepository, IUnitOfWork? unitOfWork = null)
    {
        if (context.Student.WeekStartsOn is not DayOfWeek weekStartsOn)
            return Error.Validation("ParentOverviewWeek.StudentWeekStartsOnRequired", "The student must have a week start day configured.");

        // Historical records retain their original boundary when WeekStartsOn changes.
        // Current/future creation therefore looks up only the boundary implied by the
        // student's local calendar today, rather than any selected or overlapping week.
        DateOnly currentStart = ParentWeekdayOrder.GetWeekStart(studentLocalTime.GetToday(context.Student), weekStartsOn);
        StudentWeek? current = await weekRepository.FirstOrDefaultAsync(
            week => week.StudentId == context.Student.Id && week.StartsOn == currentStart);
        if (current is null)
        {
            current = new StudentWeek(context.Student.Id, currentStart);
            weekRepository.Add(current);
            if (unitOfWork is not null) await unitOfWork.SaveChangesAsync();
        }
        return current;
    }

}
