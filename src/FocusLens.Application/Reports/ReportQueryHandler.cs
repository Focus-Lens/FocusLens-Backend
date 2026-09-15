using FocusLens.Contracts.Reports;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using FocusLens.Domain.Students;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Reports;

public sealed class ReportQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<StudySession> sessionRepository,
    ICurrentUser currentUser,
    ReportSessionMetricsCalculator metricsCalculator)
    : IRequestHandler<GetReportSessionsQuery, ReportSessionListResponse?>,
      IRequestHandler<GetReportSessionDetailQuery, ReportSessionDetailResponse?>
{
    public async Task<ReportSessionListResponse?> Handle(
        GetReportSessionsQuery request,
        CancellationToken cancellationToken)
    {
        Student? student = await GetAuthorizedStudentAsync(request.StudentId);
        if (student is null || (request.DateFrom is not null && request.DateTo is not null && request.DateFrom > request.DateTo))
        {
            return null;
        }

        StudySessionStatus? status = ParseStatus(request.Status);
        if (request.Status is not null && status is null)
        {
            return null;
        }

        List<StudySession> sessions = (await sessionRepository.GetAllAsync(
                session => session.StudentId == student.Id &&
                           session.StartedAtUtc != null &&
                           session.Status != StudySessionStatus.Active &&
                           session.Status != StudySessionStatus.Draft &&
                           session.Status != StudySessionStatus.Ready,
                session => session.Material!,
                session => session.Selection!.SelectedSections,
                session => session.CompletedSections))
            .Where(session => MatchesFilters(session, request, status))
            .OrderByDescending(session => session.StartedAtUtc)
            .ToList();

        IReadOnlyDictionary<Guid, ReportSessionMetrics> metrics = await metricsCalculator.CalculateAsync(sessions);
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        return new ReportSessionListResponse(
            sessions.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(session => ToListItem(session, metrics[session.Id], student))
                .ToArray(),
            page,
            pageSize,
            sessions.Count);
    }

    public async Task<ReportSessionDetailResponse?> Handle(
        GetReportSessionDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId == Guid.Empty)
        {
            return null;
        }

        Student? student = await GetAuthorizedStudentAsync(request.StudentId);
        if (student is null)
        {
            return null;
        }

        StudySession? session = await sessionRepository.FirstOrDefaultAsync(
            item => item.Id == request.SessionId &&
                    item.StudentId == student.Id &&
                    item.StartedAtUtc != null &&
                    (item.Status == StudySessionStatus.Paused ||
                     item.Status == StudySessionStatus.Completed ||
                     item.Status == StudySessionStatus.Cancelled),
            item => item.Material!,
            item => item.Selection!.SelectedSections,
            item => item.CompletedSections);

        if (session is null)
        {
            return null;
        }

        ReportSessionMetrics metrics = (await metricsCalculator.CalculateAsync([session]))[session.Id];
        return ToDetail(session, metrics, student);
    }

    private async Task<Student?> GetAuthorizedStudentAsync(Guid? requestedStudentId)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        Student? ownStudent = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == userId,
            student => student.Subjects);
        if (ownStudent is not null)
        {
            return requestedStudentId is null || requestedStudentId == ownStudent.Id
                ? ownStudent
                : null;
        }

        if (requestedStudentId is not Guid studentId || studentId == Guid.Empty)
        {
            return null;
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);
        if (parent is null)
        {
            return null;
        }

        ParentStudentRelationship? relationship = await relationshipRepository.FirstOrDefaultAsync(item =>
            item.ParentId == parent.Id &&
            item.StudentId == studentId &&
            item.Status == RelationshipStatus.Active);

        if (relationship is null)
        {
            return null;
        }

        Student? connectedStudent = await studentRepository.FirstOrDefaultAsync(
            student => student.Id == studentId,
            student => student.Subjects);

        return connectedStudent?.ShareSessionSummariesWithParents == true
            ? connectedStudent
            : null;
    }

    private static bool MatchesFilters(StudySession session, GetReportSessionsQuery request, StudySessionStatus? status)
    {
        DateOnly startedDate = DateOnly.FromDateTime(session.StartedAtUtc!.Value.UtcDateTime);
        return (request.DateFrom is null || startedDate >= request.DateFrom) &&
               (request.DateTo is null || startedDate <= request.DateTo) &&
               (request.SubjectId is null || session.SelectedSubjectId == request.SubjectId) &&
               (status is null || session.Status == status);
    }

    private static StudySessionStatus? ParseStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<StudySessionStatus>(value, true, out StudySessionStatus parsed) &&
              parsed is StudySessionStatus.Paused or StudySessionStatus.Completed or StudySessionStatus.Cancelled
                ? parsed
                : null;

    private static ReportSessionListItemResponse ToListItem(StudySession session, ReportSessionMetrics metrics, Student student) =>
        new(session.Id, session.StartedAtUtc!.Value, GetSubjectName(student, session.SelectedSubjectId),
            session.Material?.FileName ?? string.Empty, session.Mode.ToString(), session.Status.ToString(),
            metrics.DurationMinutes, metrics.CompletionPercentage, metrics.QuestionCount, metrics.CorrectQuestions,
            metrics.Attempts, metrics.LearningPercentage);

    private static ReportSessionDetailResponse ToDetail(StudySession session, ReportSessionMetrics metrics, Student student) =>
        new(session.Id, session.StartedAtUtc!.Value, GetSubjectName(student, session.SelectedSubjectId),
            session.Material?.FileName ?? string.Empty, session.Mode.ToString(), session.Status.ToString(),
            metrics.DurationMinutes, metrics.CompletionPercentage, metrics.QuestionCount, metrics.CorrectQuestions,
            metrics.Attempts, metrics.LearningPercentage, metrics.CompletedSections, metrics.TotalSelectedSections);

    private static string GetSubjectName(Student student, Guid? subjectId)
    {
        StudentSubject? subject = student.Subjects.FirstOrDefault(item => item.Id == subjectId);
        if (subject is null)
        {
            return "Unknown subject";
        }

        return subject.Type == StudentSubjectType.Other
            ? subject.CustomName ?? "Other"
            : subject.Type.ToString();
    }
}
