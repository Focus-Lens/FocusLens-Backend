using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class GetParentDashboardSessionsQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
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
                session.StartedAtUtc != null))
            .ToArray();

        StudySession[] matchingSessions = ParentDashboardHelpers
            .ApplyFilters(startedSessions, filtersResult.Value)
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
                totalPages));
    }
}
