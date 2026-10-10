using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Services;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class ExportParentDashboardSessionsQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudentWeek> weekRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IUnitOfWork? unitOfWork = null,
    IStudentLocalTime? studentLocalTime = null)
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

        studentLocalTime ??= new StudentLocalTime(timeProvider);
        Result<ParentOverviewWeekResponse> week =
            await OverviewWeekResolver.ResolveAsync(contextResult.Value, studentLocalTime, weekRepository, unitOfWork);
        if (week.IsError)
        {
            return week.Errors;
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
                    session.StartedAtUtc != null,
                session => session.PauseIntervals))
            .ToArray();

        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        IReadOnlyDictionary<Guid, string?> subjectNamesById =
            ParentDashboardHelpers.GetSubjectNamesById(contextResult.Value.Student);

        StudySession[] sessions = ParentDashboardHelpers
            .OrderSessions(ParentDashboardHelpers.ApplyFilters(startedSessions.Where(session =>
                studentLocalTime.GetLocalDate(session.StartedAtUtc!.Value, contextResult.Value.Student) >=
                week.Value.StartsOn &&
                studentLocalTime.GetLocalDate(session.StartedAtUtc!.Value, contextResult.Value.Student) <=
                week.Value.EndsOn), filtersResult.Value))
            .ToArray();

        return ParentDashboardHelpers.CreateRawCsvExport(request.StudentId, sessions, subjectNamesById, utcNow);
    }
}