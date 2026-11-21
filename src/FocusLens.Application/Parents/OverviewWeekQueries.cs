using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Services;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed record SetParentOverviewWeekCommand(Guid StudentId, DateOnly? WeekStart)
    : IRequest<Result<ParentOverviewWeekResponse>>;

public sealed record GetParentOverviewWeeksQuery(Guid StudentId) : IRequest<Result<ParentOverviewWeeksResponse>>;

public sealed class ParentOverviewWeekHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudentWeek> weekRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IUnitOfWork? unitOfWork = null,
    IStudentLocalTime? studentLocalTime = null)
    : IRequestHandler<SetParentOverviewWeekCommand, Result<ParentOverviewWeekResponse>>,
        IRequestHandler<GetParentOverviewWeeksQuery, Result<ParentOverviewWeeksResponse>>
{
    public async Task<Result<ParentOverviewWeeksResponse>> Handle(GetParentOverviewWeeksQuery request,
        CancellationToken cancellationToken)
    {
        Result<ParentDashboardContext> context = await Context(request.StudentId);
        if (context.IsError)
        {
            return context.Errors;
        }

        Result<StudentWeek> current =
            await OverviewWeekResolver.ResolveActualCurrentWeekAsync(context.Value, LocalTime(), weekRepository,
                unitOfWork);
        if (current.IsError)
        {
            return current.Errors;
        }

        Result<DayOfWeek> weekStartsOn = OverviewWeekResolver.GetParentWeekStartsOn(context.Value);
        if (weekStartsOn.IsError)
        {
            return weekStartsOn.Errors;
        }

        // DateOnly.DayOfWeek is not translated by every provider. More importantly,
        // stored StudentWeek records can use an older/student calendar boundary. Read
        // all of this student's historical weeks, then project them into the parent's
        // calendar in memory without mutating the underlying records.
        DateOnly[] starts = (await weekRepository.GetAllAsync(week => week.StudentId == request.StudentId))
            .Select(week => OverviewWeekResolver.NormalizeToParentWeekStart(week.StartsOn, weekStartsOn.Value))
            .Append(current.Value.StartsOn)
            .Distinct()
            .OrderBy(start => start)
            .ToArray();

        return new ParentOverviewWeeksResponse(starts
            .Select(start => new ParentOverviewWeekResponse(
                start,
                start.AddDays(6),
                start == current.Value.StartsOn))
            .ToArray());
    }

    public async Task<Result<ParentOverviewWeekResponse>> Handle(SetParentOverviewWeekCommand request,
        CancellationToken cancellationToken)
    {
        Result<ParentDashboardContext> context = await Context(request.StudentId);
        if (context.IsError)
        {
            return context.Errors;
        }

        if (request.WeekStart is DateOnly weekStart)
        {
            Result<DayOfWeek> parentWeekStartsOn = OverviewWeekResolver.GetParentWeekStartsOn(context.Value);
            if (parentWeekStartsOn.IsError)
            {
                return parentWeekStartsOn.Errors;
            }

            if (weekStart != OverviewWeekResolver.NormalizeToParentWeekStart(weekStart, parentWeekStartsOn.Value))
            {
                return Error.Validation("ParentOverviewWeek.InvalidWeekStart",
                    "The selected week must start on the parent's configured week start day.");
            }

            StudentWeek[] historicalWeeks = (await weekRepository.GetAllAsync(week =>
                week.StudentId == request.StudentId)).ToArray();
            Result<StudentWeek> current = await OverviewWeekResolver.ResolveActualCurrentWeekAsync(
                context.Value, LocalTime(), weekRepository, unitOfWork);
            if (current.IsError)
            {
                return current.Errors;
            }

            bool exists = historicalWeeks.Any(week =>
                              OverviewWeekResolver.NormalizeToParentWeekStart(
                                  week.StartsOn, parentWeekStartsOn.Value) == weekStart) ||
                          current.Value.StartsOn == weekStart;
            if (!exists)
            {
                return Error.Validation("ParentOverviewWeek.NotFound", "The requested overview week does not exist.");
            }
        }

        context.Value.Relationship.SetSelectedOverviewWeekStart(request.WeekStart);
        await relationshipRepository.UpdateAsync(context.Value.Relationship);
        if (unitOfWork is not null)
        {
            await unitOfWork.SaveChangesAsync();
        }

        return await OverviewWeekResolver.ResolveAsync(context.Value, LocalTime(), weekRepository, unitOfWork);
    }

    private async Task<Result<ParentDashboardContext>> Context(Guid studentId) =>
        await ParentDashboardHelpers.ResolveContextAsync(studentId, parentRepository, relationshipRepository,
            studentRepository, currentUser);

    private IStudentLocalTime LocalTime() => studentLocalTime ??= new StudentLocalTime(timeProvider);
}
