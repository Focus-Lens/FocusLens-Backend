using System.Text;
using System.Text.Json;
using FocusLens.Application.Reports;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class ExportStudentDataQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> sessionRepository,
    ReportSessionMetricsCalculator metricsCalculator,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<ExportStudentDataQuery, StudentDataExport>
{
    public async Task<StudentDataExport> Handle(
        ExportStudentDataQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user could not be identified.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId,
            item => item.User,
            item => item.Subjects);
        if (student is null)
        {
            throw new KeyNotFoundException("The student profile was not found.");
        }

        StudySession[] sessions = (await sessionRepository.GetAllAsync(
                session => session.StudentId == student.Id &&
                           session.StartedAtUtc != null &&
                           session.Status != StudySessionStatus.Draft &&
                           session.Status != StudySessionStatus.Ready,
                session => session.Selection!.SelectedSections,
                session => session.CompletedSections))
            .OrderByDescending(session => session.StartedAtUtc)
            .ToArray();
        IReadOnlyDictionary<Guid, ReportSessionMetrics> metrics =
            await metricsCalculator.CalculateAsync(sessions);

        string GetSubjectName(Guid? subjectId)
        {
            StudentSubject? subject = student.Subjects.FirstOrDefault(item => item.Id == subjectId);
            return subject is null
                ? "Unknown subject"
                : subject.Type == StudentSubjectType.Other
                    ? subject.CustomName ?? "Other"
                    : subject.Type.ToString();
        }

        var subjectProgress = sessions
            .GroupBy(session => session.SelectedSubjectId)
            .Select(group =>
            {
                ReportSessionMetrics[] values = group.Select(session => metrics[session.Id]).ToArray();
                int totalSections = values.Sum(value => value.TotalSelectedSections);
                int completedSections = values.Sum(value => value.CompletedSections);
                int totalQuestions = values.Sum(value => value.QuestionCount);
                int correctQuestions = values.Sum(value => value.CorrectQuestions);

                return new
                {
                    subject = GetSubjectName(group.Key),
                    sessions = group.Count(),
                    durationMinutes = values.Sum(value => value.DurationMinutes),
                    completionPercentage = totalSections == 0
                        ? 0
                        : (int)Math.Round(completedSections * 100d / totalSections),
                    learningPercentage = totalQuestions == 0
                        ? 0
                        : (int)Math.Round(correctQuestions * 100d / totalQuestions)
                };
            })
            .ToArray();

        var export = new
        {
            student = new
            {
                student.Id,
                student.User.FirstName,
                student.User.LastName,
                student.User.Email,
                student.User.PhoneNumber,
                student.PreferredName,
                student.DateOfBirth,
                student.Grade,
                subjects = student.Subjects.Select(subject => new { subject.Id, subject.Type, subject.CustomName }),
                student.ShareSessionSummariesWithParents,
                student.ShareSubjectTrendsWithParents
            },
            sessions = sessions.Select(session => new
            {
                session.Id,
                session.StartedAtUtc,
                session.CompletedAtUtc,
                session.CancelledAtUtc,
                session.Status,
                session.Mode,
                session.SelectedSubjectId,
                subject = GetSubjectName(session.SelectedSubjectId),
                session.FocusDurationMinutes,
                session.AccumulatedPausedSeconds,
                summary = metrics[session.Id]
            }),
            subjectProgress,
            exportedAtUtc = timeProvider.GetUtcNow()
        };

        byte[] content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(export));
        return new StudentDataExport(
            $"focuslens-student-data-{student.Id:N}.json",
            "application/json; charset=utf-8",
            content);
    }
}