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

        StudentWeek[] weeks = (await weekRepository.GetAllAsync(week => week.StudentId == request.StudentId)).ToArray();
        return new ParentOverviewWeeksResponse(weeks.OrderBy(week => week.StartsOn)
            .Select(week => new ParentOverviewWeekResponse(
                week.StartsOn,
                week.EndsOn,
                week.StartsOn == current.Value.StartsOn && week.EndsOn == current.Value.EndsOn))
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
            if (await weekRepository.FirstOrDefaultAsync(week =>
                    week.StudentId == request.StudentId && week.StartsOn == weekStart) is null)
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