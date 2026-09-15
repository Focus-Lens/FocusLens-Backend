using System.Globalization;
using System.Text;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using DomainStudentSubjectType = FocusLens.Domain.Students.StudentSubjectType;

namespace FocusLens.Application.Parents;

internal static class ParentDashboardHelpers
{
    public const int PulseDays = 7;
    public const int MaxPageSize = 100;
    public const string CsvContentType = "text/csv; charset=utf-8";

    public static async Task<Result<ParentDashboardContext>> ResolveContextAsync(
        Guid studentId,
        IBaseRepository<Parent> parentRepository,
        IBaseRepository<ParentStudentRelationship> relationshipRepository,
        IBaseRepository<Student> studentRepository,
        ICurrentUser currentUser)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "ParentDashboard.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        if (studentId == Guid.Empty)
        {
            return Error.Validation(
                "ParentDashboard.InvalidStudentId",
                "Student ID is required.");
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            parent => parent.UserId == userId,
            parent => parent.User);

        if (parent is null)
        {
            return Error.NotFound(
                "ParentDashboard.ParentNotFound",
                "The parent profile was not found.");
        }

        ParentStudentRelationship? relationship =
            await relationshipRepository.FirstOrDefaultAsync(relationship =>
                relationship.ParentId == parent.Id &&
                relationship.StudentId == studentId &&
                relationship.Status == RelationshipStatus.Active);

        if (relationship is null)
        {
            return Error.Forbidden(
                "ParentDashboard.RelationshipRequired",
                "You can only view dashboard data for an active student relationship.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.Id == studentId,
            student => student.Subjects);

        if (student is null)
        {
            return Error.NotFound(
                "ParentDashboard.StudentNotFound",
                "The student profile was not found.");
        }

        return new ParentDashboardContext(parent, student);
    }

    public static Result<ParentDashboardSessionFilters> CreateFilters(
        IReadOnlyCollection<string>? statuses,
        Guid? subjectId,
        int? page = null,
        int? pageSize = null)
    {
        List<Error> errors = [];

        if (page is <= 0)
        {
            errors.Add(Error.Validation(
                "ParentDashboard.InvalidPage",
                "Page must be greater than zero."));
        }

        if (pageSize is <= 0 or > MaxPageSize)
        {
            errors.Add(Error.Validation(
                "ParentDashboard.InvalidPageSize",
                $"Page size must be between 1 and {MaxPageSize}."));
        }

        if (subjectId == Guid.Empty)
        {
            errors.Add(Error.Validation(
                "ParentDashboard.InvalidSubjectId",
                "Subject ID must be a valid non-empty ID."));
        }

        StudySessionStatus[] parsedStatuses = [];
        if (statuses is not null)
        {
            parsedStatuses = statuses
                .Where(status => !string.IsNullOrWhiteSpace(status))
                .Select(status =>
                {
                    bool parsed = Enum.TryParse(status, ignoreCase: false, out StudySessionStatus value) &&
                                  Enum.IsDefined(value);

                    if (!parsed)
                    {
                        errors.Add(Error.Validation(
                            "ParentDashboard.InvalidSessionStatus",
                            $"Session status '{status}' is invalid."));
                    }

                    return value;
                })
                .Distinct()
                .ToArray();

            if (statuses.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add(Error.Validation(
                    "ParentDashboard.InvalidSessionStatus",
                    "Session status is required when the status filter is supplied."));
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return new ParentDashboardSessionFilters(parsedStatuses, subjectId);
    }

    public static IEnumerable<StudySession> ApplyFilters(
        IEnumerable<StudySession> sessions,
        ParentDashboardSessionFilters filters)
    {
        IEnumerable<StudySession> query = sessions;

        if (filters.Statuses.Count > 0)
        {
            HashSet<StudySessionStatus> statuses = filters.Statuses.ToHashSet();
            query = query.Where(session => statuses.Contains(session.Status));
        }

        if (filters.SubjectId is Guid subjectId)
        {
            query = query.Where(session => session.SelectedSubjectId == subjectId);
        }

        return query;
    }

    public static IOrderedEnumerable<StudySession> OrderSessions(IEnumerable<StudySession> sessions) =>
        sessions
            .OrderByDescending(session => session.StartedAtUtc)
            .ThenByDescending(session => session.Id);

    public static ParentDashboardStudySessionResponse ToSessionResponse(
        StudySession session,
        IReadOnlyDictionary<Guid, string?> subjectNamesById,
        DateTimeOffset utcNow)
    {
        string? subjectName = session.SelectedSubjectId is Guid subjectId &&
                              subjectNamesById.TryGetValue(subjectId, out string? name)
            ? name
            : null;

        return new ParentDashboardStudySessionResponse(
            session.Id,
            session.Status.ToString(),
            session.Mode.ToString(),
            session.StartedAtUtc!.Value,
            session.CompletedAtUtc,
            session.CancelledAtUtc,
            session.LastActivityAtUtc,
            session.SelectedSubjectId,
            subjectName,
            GetActualStudyMinutes(session, utcNow),
            session.FocusDurationMinutes);
    }

    public static IReadOnlyDictionary<Guid, string?> GetSubjectNamesById(Student student) =>
        student.Subjects.ToDictionary(subject => subject.Id, GetSubjectName);

    public static int GetActualStudyMinutesForDates(
        IEnumerable<StudySession> sessions,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTimeOffset utcNow)
    {
        return sessions
            .Where(session =>
            {
                DateOnly sessionDate = DateOnly.FromDateTime(session.StartedAtUtc!.Value.UtcDateTime);
                return sessionDate >= startsOn && sessionDate <= endsOn;
            })
            .Sum(session => GetActualStudyMinutes(session, utcNow));
    }

    public static int GetActualStudyMinutes(StudySession session, DateTimeOffset utcNow)
    {
        TimeSpan actualStudyTime = GetEffectiveEnd(session, utcNow) -
                                   session.StartedAtUtc!.Value -
                                   TimeSpan.FromSeconds(session.AccumulatedPausedSeconds);

        if (actualStudyTime <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Ceiling(actualStudyTime.TotalMinutes);
    }

    public static ParentDashboardSessionsCsvExport CreateCsvExport(
        Guid studentId,
        IEnumerable<ParentDashboardStudySessionResponse> sessions,
        DateTimeOffset utcNow)
    {
        var builder = new StringBuilder();
        AppendCsvRow(
            builder,
            [
                "Id",
                "StartedAtUtc",
                "Status",
                "Mode",
                "SelectedSubjectId",
                "SubjectName",
                "ActualStudyMinutes",
                "PlannedFocusDurationMinutes",
                "CompletedAtUtc",
                "CancelledAtUtc",
                "LastActivityAtUtc"
            ]);

        foreach (ParentDashboardStudySessionResponse session in sessions)
        {
            AppendCsvRow(
                builder,
                [
                    session.Id.ToString(),
                    FormatDateTimeOffset(session.StartedAtUtc),
                    session.Status,
                    session.Mode,
                    session.SelectedSubjectId?.ToString() ?? string.Empty,
                    SanitizeCsvTextValue(session.SubjectName ?? string.Empty),
                    session.ActualStudyMinutes.ToString(CultureInfo.InvariantCulture),
                    session.PlannedFocusDurationMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    FormatDateTimeOffset(session.CompletedAtUtc),
                    FormatDateTimeOffset(session.CancelledAtUtc),
                    FormatDateTimeOffset(session.LastActivityAtUtc)
                ]);
        }

        string timestamp = utcNow.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return new ParentDashboardSessionsCsvExport(
            $"parent-dashboard-sessions-{studentId:N}-{timestamp}.csv",
            CsvContentType,
            Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static DateTimeOffset GetEffectiveEnd(StudySession session, DateTimeOffset utcNow)
    {
        if (session.CompletedAtUtc is not null)
        {
            return session.CompletedAtUtc.Value;
        }

        if (session.CancelledAtUtc is not null)
        {
            return session.CancelledAtUtc.Value;
        }

        if (session.PausedAtUtc is not null)
        {
            return session.PausedAtUtc.Value;
        }

        return utcNow;
    }

    private static string? GetSubjectName(StudentSubject subject)
    {
        return subject.Type == DomainStudentSubjectType.Other
            ? subject.CustomName
            : subject.Type.ToString();
    }

    private static void AppendCsvRow(StringBuilder builder, IReadOnlyCollection<string> values)
    {
        builder.AppendJoin(',', values.Select(EscapeCsvValue));
        builder.AppendLine();
    }

    private static string EscapeCsvValue(string value)
    {
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string SanitizeCsvTextValue(string value)
    {
        return value.StartsWith('=') ||
               value.StartsWith('+') ||
               value.StartsWith('-') ||
               value.StartsWith('@')
            ? $"'{value}"
            : value;
    }

    private static string FormatDateTimeOffset(DateTimeOffset? value) =>
        value?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
}

internal sealed record ParentDashboardContext(Parent Parent, Student Student);

internal sealed record ParentDashboardSessionFilters(
    IReadOnlyCollection<StudySessionStatus> Statuses,
    Guid? SubjectId);
