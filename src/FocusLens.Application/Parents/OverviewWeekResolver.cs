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
        if (context.Student.WeekStartsOn is not DayOfWeek weekStartsOn)
            return Error.Validation("ParentOverviewWeek.StudentWeekStartsOnRequired", "The student must have a week start day configured.");

        DateOnly today = studentLocalTime.GetToday(context.Student);
        DateOnly? persistedStart = context.Relationship.SelectedOverviewWeekStart;
        StudentWeek? selected = persistedStart is null ? null : await weekRepository.FirstOrDefaultAsync(
            week => week.StudentId == context.Student.Id && week.StartsOn == persistedStart.Value);
        if (persistedStart is not null && selected is null)
            return Error.Validation("ParentOverviewWeek.NotFound", "The selected overview week no longer exists.");

        // Historical records retain their original boundary when WeekStartsOn changes.
        // Current/future creation therefore looks up only the boundary implied today,
        // rather than treating any overlapping historical record as the current week.
        DateOnly currentStart = ParentWeekdayOrder.GetWeekStart(today, weekStartsOn);
        StudentWeek? current = selected ?? await weekRepository.FirstOrDefaultAsync(
            week => week.StudentId == context.Student.Id && week.StartsOn == currentStart);
        if (current is null)
        {
            current = new StudentWeek(context.Student.Id, currentStart);
            weekRepository.Add(current);
            if (unitOfWork is not null) await unitOfWork.SaveChangesAsync();
        }
        return new ParentOverviewWeekResponse(current.StartsOn, current.EndsOn, current.StartsOn <= today && current.EndsOn >= today);
    }

}
