using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class ExportParentDashboardSessionsQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<ExportParentDashboardSessionsQuery, Result<ParentDashboardSessionsCsvExport>>
{
    public async Task<Result<ParentDashboardSessionsCsvExport>> Handle(
        ExportParentDashboardSessionsQuery request,
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
            request.SubjectId);

        if (filtersResult.IsError)
        {
            return filtersResult.Errors;
        }

        StudySession[] startedSessions = (await studySessionRepository.GetAllAsync(session =>
                session.StudentId == request.StudentId &&
                session.StartedAtUtc != null))
            .ToArray();

        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        IReadOnlyDictionary<Guid, string?> subjectNamesById =
            ParentDashboardHelpers.GetSubjectNamesById(contextResult.Value.Student);

        ParentDashboardStudySessionResponse[] sessions = ParentDashboardHelpers
            .OrderSessions(ParentDashboardHelpers.ApplyFilters(startedSessions, filtersResult.Value))
            .Select(session => ParentDashboardHelpers.ToSessionResponse(session, subjectNamesById, utcNow))
            .ToArray();

        return ParentDashboardHelpers.CreateCsvExport(request.StudentId, sessions, utcNow);
    }
}
