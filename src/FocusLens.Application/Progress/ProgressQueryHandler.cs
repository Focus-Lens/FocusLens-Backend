using FocusLens.Application.Reports;
using FocusLens.Contracts.Progress;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using FocusLens.Domain.Students;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Progress;

public sealed class ProgressQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<StudySession> sessionRepository,
    ReportSessionMetricsCalculator metricsCalculator,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetProgressQuery, ProgressResponse?>
{
    // v1 intentionally evaluates sufficiency inside the selected range.
    private const int EnoughDataSessionCount = 3;

    public async Task<ProgressResponse?> Handle(GetProgressQuery request, CancellationToken cancellationToken)
    {
        Student? student = await GetAuthorizedStudentAsync(request.StudentId);
        DateRange? range = ResolveRange(request);
        if (student is null || range is null)
        {
            return null;
        }

        List<StudySession> sessions = (await sessionRepository.GetAllAsync(
                session => session.StudentId == student.Id &&
                           session.StartedAtUtc != null &&
                           session.Status != StudySessionStatus.Active &&
                           session.Status != StudySessionStatus.Draft &&
                           session.Status != StudySessionStatus.Ready,
                session => session.Selection!.SelectedSections,
                session => session.CompletedSections))
            .Where(session => IsWithinRange(session.StartedAtUtc!.Value, range))
            .ToList();

        IReadOnlyDictionary<Guid, ReportSessionMetrics> metrics = await metricsCalculator.CalculateAsync(sessions);
        ProgressDailyItemResponse[] daily = Enumerable.Range(0, range.DateTo.DayNumber - range.DateFrom.DayNumber + 1)
            .Select(offset => range.DateFrom.AddDays(offset))
            .Select(date => CreateDaily(date, sessions, metrics))
            .ToArray();
        ProgressSubjectItemResponse[] subjects = sessions
            .GroupBy(session => session.SelectedSubjectId)
            .Select(group => CreateSubject(group.Key, group.ToArray(), metrics, student))
            .OrderByDescending(item => item.ActualStudyMinutes)
            .ThenBy(item => item.Subject)
            .ToArray();

        int totalQuestions = sessions.Sum(session => metrics[session.Id].QuestionCount);
        int correctQuestions = sessions.Sum(session => metrics[session.Id].CorrectQuestions);

        return new ProgressResponse(
            range.DateFrom,
            range.DateTo,
            sessions.Count,
            sessions.Sum(session => metrics[session.Id].DurationMinutes),
            CalculatePercentage(correctQuestions, totalQuestions),
            ToDataStatus(sessions.Count),
            daily,
            subjects);
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
            return requestedStudentId is null || requestedStudentId == ownStudent.Id ? ownStudent : null;
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
            item.ParentId == parent.Id && item.StudentId == studentId && item.Status == RelationshipStatus.Active);
        return relationship is null
            ? null
            : await studentRepository.FirstOrDefaultAsync(student => student.Id == studentId, student => student.Subjects);
    }

    private DateRange? ResolveRange(GetProgressQuery request)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        string range = string.IsNullOrWhiteSpace(request.Range) ? "Last30Days" : request.Range.Trim();
        return range.ToLowerInvariant() switch
        {
            "last7days" => new DateRange(today.AddDays(-6), today),
            "last30days" => new DateRange(today.AddDays(-29), today),
            "custom" when request.DateFrom is not null && request.DateTo is not null && request.DateFrom <= request.DateTo =>
                new DateRange(request.DateFrom.Value, request.DateTo.Value),
            _ => null
        };
    }

    private static bool IsWithinRange(DateTimeOffset startedAtUtc, DateRange range)
    {
        // v1 groups calendar days in UTC. A user-timezone contract can replace this when introduced.
        DateOnly date = DateOnly.FromDateTime(startedAtUtc.UtcDateTime);
        return date >= range.DateFrom && date <= range.DateTo;
    }

    private static ProgressDailyItemResponse CreateDaily(
        DateOnly date,
        IEnumerable<StudySession> sessions,
        IReadOnlyDictionary<Guid, ReportSessionMetrics> metrics)
    {
        StudySession[] matching = sessions.Where(session =>
            DateOnly.FromDateTime(session.StartedAtUtc!.Value.UtcDateTime) == date).ToArray();
        return new ProgressDailyItemResponse(date, matching.Length,
            matching.Sum(session => metrics[session.Id].DurationMinutes));
    }

    private static ProgressSubjectItemResponse CreateSubject(
        Guid? subjectId,
        IReadOnlyCollection<StudySession> sessions,
        IReadOnlyDictionary<Guid, ReportSessionMetrics> metrics,
        Student student)
    {
        int totalSections = sessions.Sum(session => metrics[session.Id].TotalSelectedSections);
        int completedSections = sessions.Sum(session => metrics[session.Id].CompletedSections);
        int questions = sessions.Sum(session => metrics[session.Id].QuestionCount);
        int correctQuestions = sessions.Sum(session => metrics[session.Id].CorrectQuestions);
        return new ProgressSubjectItemResponse(
            subjectId,
            GetSubjectName(student, subjectId),
            sessions.Count,
            sessions.Sum(session => metrics[session.Id].DurationMinutes),
            CalculatePercentage(completedSections, totalSections),
            CalculatePercentage(correctQuestions, questions),
            ToDataStatus(sessions.Count));
    }

    private static int CalculatePercentage(int numerator, int denominator) => denominator == 0
        ? 0
        : (int)Math.Round(numerator * 100d / denominator, MidpointRounding.AwayFromZero);

    private static ProgressDataStatus ToDataStatus(int sessionCount) =>
        sessionCount >= EnoughDataSessionCount ? ProgressDataStatus.EnoughData : ProgressDataStatus.InsufficientData;

    private static string GetSubjectName(Student student, Guid? subjectId)
    {
        StudentSubject? subject = student.Subjects.FirstOrDefault(item => item.Id == subjectId);
        return subject is null ? "Unknown subject" : subject.Type == StudentSubjectType.Other
            ? subject.CustomName ?? "Other"
            : subject.Type.ToString();
    }

    private sealed record DateRange(DateOnly DateFrom, DateOnly DateTo);
}
