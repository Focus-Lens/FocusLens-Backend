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
    IBaseRepository<StudySessionBehaviorWindow> behaviorWindowRepository,
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

        StudySession[] paginatedSessions = ParentDashboardHelpers
            .OrderSessions(matchingSessions)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArray();

        IReadOnlyDictionary<Guid, StudySessionBehaviorWindow> latestWindows = await GetLatestWindowsAsync(paginatedSessions);
        ParentDashboardStudySessionResponse[] pageSessions = paginatedSessions
            .Select(session => ParentDashboardHelpers.ToSessionResponse(
                session,
                subjectNamesById,
                utcNow,
                latestWindows.GetValueOrDefault(session.Id)))
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

    private async Task<IReadOnlyDictionary<Guid, StudySessionBehaviorWindow>> GetLatestWindowsAsync(
        IReadOnlyCollection<StudySession> sessions)
    {
        Guid[] sessionIds = sessions.Select(session => session.Id).ToArray();
        if (sessionIds.Length == 0)
        {
            return new Dictionary<Guid, StudySessionBehaviorWindow>();
        }

        return (await behaviorWindowRepository.GetAllAsync(item => sessionIds.Contains(item.StudySessionId)))
            .GroupBy(item => item.StudySessionId)
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(item => item.WindowIndex)
                .First());
    }
}
