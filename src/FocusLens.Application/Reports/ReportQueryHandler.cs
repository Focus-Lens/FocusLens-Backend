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
    IBaseRepository<StudySessionBehaviorWindow> behaviorWindowRepository,
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
                session => session.CompletedSections,
                session => session.PauseIntervals))
            .Where(session => MatchesFilters(session, request, status))
            .OrderByDescending(session => session.StartedAtUtc)
            .ToList();

        IReadOnlyDictionary<Guid, ReportSessionMetrics> metrics = await metricsCalculator.CalculateAsync(sessions);
        IReadOnlyDictionary<Guid, StudySessionBehaviorWindow> latestWindows = await GetLatestWindowsAsync(sessions);
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        return new ReportSessionListResponse(
            sessions.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(session => ToListItem(session, metrics[session.Id], student,
                    latestWindows.GetValueOrDefault(session.Id)))
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
            item => item.CompletedSections,
            item => item.PauseIntervals);

        if (session is null)
        {
            return null;
        }

        ReportSessionMetrics metrics = (await metricsCalculator.CalculateAsync([session]))[session.Id];
        StudySessionBehaviorWindow? latestWindow = (await GetLatestWindowsAsync([session]))
            .GetValueOrDefault(session.Id);
        return ToDetail(session, metrics, student, latestWindow);
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

    private static ReportSessionListItemResponse ToListItem(
        StudySession session,
        ReportSessionMetrics metrics,
        Student student,
        StudySessionBehaviorWindow? behaviorWindow) =>
        new(session.Id, session.StartedAtUtc!.Value, GetSubjectName(student, session.SelectedSubjectId),
            session.Material?.FileName ?? string.Empty, session.Mode.ToString(), session.Status.ToString(),
            metrics.DurationMinutes, metrics.CompletionPercentage, metrics.QuestionCount, metrics.CorrectQuestions,
            metrics.Attempts, metrics.LearningPercentage, behaviorWindow?.FocusScore, behaviorWindow?.FocusState,
            behaviorWindow?.UnderstandingScore, behaviorWindow?.UnderstandingTrend);

    private static ReportSessionDetailResponse ToDetail(
        StudySession session,
        ReportSessionMetrics metrics,
        Student student,
        StudySessionBehaviorWindow? behaviorWindow)
    {
        string summary = CreateSummary(metrics, behaviorWindow);
        string[] highlights = CreateHighlights(session, metrics, behaviorWindow);
        return new ReportSessionDetailResponse(
            session.Id, session.StartedAtUtc!.Value, GetSubjectName(student, session.SelectedSubjectId),
            session.Material?.FileName ?? string.Empty, session.Mode.ToString(), session.Status.ToString(),
            metrics.DurationMinutes, metrics.CompletionPercentage, metrics.QuestionCount, metrics.CorrectQuestions,
            metrics.Attempts, metrics.LearningPercentage, metrics.CompletedSections, metrics.TotalSelectedSections,
            session.PauseIntervals.Count, behaviorWindow?.FocusScore, behaviorWindow?.FocusState,
            behaviorWindow?.FocusTrend, behaviorWindow?.UnderstandingScore, behaviorWindow?.UnderstandingTrend,
            summary, highlights);
    }

    private static string CreateSummary(ReportSessionMetrics metrics, StudySessionBehaviorWindow? window)
    {
        if (window?.FocusState is not null)
        {
            return $"Focus was {window.FocusState.Replace('_', ' ').ToLowerInvariant()} in the latest analyzed study window.";
        }

        return metrics.TotalSelectedSections == 0
            ? "No selected sections were available for this session."
            : $"Completed {metrics.CompletedSections} of {metrics.TotalSelectedSections} selected study sections.";
    }

    private static string[] CreateHighlights(
        StudySession session,
        ReportSessionMetrics metrics,
        StudySessionBehaviorWindow? window)
    {
        List<string> highlights = [$"{metrics.DurationMinutes} min focused study time", $"{session.PauseIntervals.Count} pauses"];
        if (window?.FocusScore is int focus)
        {
            highlights.Add($"Focus score {focus}%");
        }

        if (window?.UnderstandingScore is int understanding)
        {
            highlights.Add($"Understanding score {understanding}%");
        }

        if (metrics.QuestionCount > 0)
        {
            highlights.Add($"Learning result {metrics.LearningPercentage}%");
        }

        return highlights.ToArray();
    }

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
