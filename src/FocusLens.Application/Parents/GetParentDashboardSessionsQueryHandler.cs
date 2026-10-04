using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using FocusLens.Domain.Students;
using FocusLens.Application.Common.Interfaces;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class GetParentDashboardSessionsQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudentWeek> weekRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IUnitOfWork? unitOfWork = null,
    IStudentLocalTime? studentLocalTime = null)
    : IRequestHandler<GetParentDashboardSessionsQuery, Result<ParentDashboardSessionHistoryResponse>>
{
    public async Task<Result<ParentDashboardSessionHistoryResponse>> Handle(
        GetParentDashboardSessionsQuery request,
        CancellationToken cancellationToken)
    {
        Result<ParentDashboardContext> contextResult = await ParentDashboardHelpers.ResolveContextAsync(
            request.StudentId,
            parentRepository,
            relationshipRepository,
            studentRepository,
            currentUser);

        if (contextResult.IsError)
        {
            return contextResult.Errors;
        }
        studentLocalTime ??= new Common.Services.StudentLocalTime(timeProvider);
        Result<ParentOverviewWeekResponse> week = await OverviewWeekResolver.ResolveAsync(contextResult.Value, studentLocalTime, weekRepository, unitOfWork);
        if (week.IsError) return week.Errors;

        if (!contextResult.Value.Student.ShareSessionSummariesWithParents)
        {
            return Error.Forbidden(
                "ParentDashboard.SessionSummariesPrivate",
                "The student has not shared session summaries with parents.");
        }

        Result<ParentDashboardSessionFilters> filtersResult = ParentDashboardHelpers.CreateFilters(
            request.Statuses,
            request.SubjectId,
            request.Page,
            request.PageSize);

        if (filtersResult.IsError)
        {
            return filtersResult.Errors;
        }

        StudySession[] startedSessions = (await studySessionRepository.GetAllAsync(session =>
                session.StudentId == request.StudentId &&
                session.StartedAtUtc != null,
                session => session.PauseIntervals))
            .ToArray();

        StudySession[] matchingSessions = ParentDashboardHelpers
            .ApplyFilters(startedSessions.Where(session =>
                studentLocalTime.GetLocalDate(session.StartedAtUtc!.Value, contextResult.Value.Student) >= week.Value.StartsOn &&
                studentLocalTime.GetLocalDate(session.StartedAtUtc!.Value, contextResult.Value.Student) <= week.Value.EndsOn), filtersResult.Value)
            .ToArray();

        int totalCount = matchingSessions.Length;
        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        IReadOnlyDictionary<Guid, string?> subjectNamesById =
            ParentDashboardHelpers.GetSubjectNamesById(contextResult.Value.Student);

        ParentDashboardStudySessionResponse[] pageSessions = ParentDashboardHelpers
            .OrderSessions(matchingSessions)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(session => ParentDashboardHelpers.ToSessionResponse(session, subjectNamesById, utcNow))
            .ToArray();

        return new ParentDashboardSessionHistoryResponse(
            pageSessions,
            new ParentDashboardPaginationResponse(
                request.Page,
                request.PageSize,
                totalCount,
                totalPages))
        {
            Week = new ParentDashboardWeekResponse(week.Value.StartsOn, week.Value.EndsOn, week.Value.IsCurrentWeek)
        };
    }
}
