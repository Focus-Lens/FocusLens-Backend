using System.Globalization;
using System.Text;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Utilities;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using DomainStudentSubjectType = FocusLens.Domain.Students.StudentSubjectType;

namespace FocusLens.Application.Parents;

internal static class ParentDashboardHelpers
{
    private const string NullCsvValue = "NULL";

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
            student => student.Subjects,
            student => student.User);

        if (student is null)
        {
            return Error.NotFound(
                "ParentDashboard.StudentNotFound",
                "The student profile was not found.");
        }

        return new ParentDashboardContext(parent, student, relationship);
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
        DateTimeOffset utcNow,
        StudySessionBehaviorWindow? behaviorWindow = null)
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
            DurationDisplayFormatter.FormatMinutes(GetActualStudyMinutes(session, utcNow)),
            session.FocusDurationMinutes is int plannedDuration
                ? DurationDisplayFormatter.FormatMinutes(plannedDuration)
                : null,
            behaviorWindow?.FocusScore,
            behaviorWindow?.FocusState,
            behaviorWindow?.FocusTrend);
    }

    public static IReadOnlyDictionary<Guid, string?> GetSubjectNamesById(Student student) =>
        student.Subjects.ToDictionary(subject => subject.Id, GetSubjectName);

    public static int GetActualStudyMinutesForDates(
        IEnumerable<StudySession> sessions,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTimeOffset utcNow)
        => GetActualStudyMinutesForDates(
            sessions,
            startsOn,
            endsOn,
            utcNow,
            session => DateOnly.FromDateTime(session.StartedAtUtc!.Value.UtcDateTime));

    public static int GetActualStudyMinutesForDates(
        IEnumerable<StudySession> sessions,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTimeOffset utcNow,
        Func<StudySession, DateOnly> getSessionDate)
    {
        return sessions
            .Where(session =>
            {
                DateOnly sessionDate = getSessionDate(session);
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

    /// <summary>Allocates active study time to the student's business-calendar days.
    /// A calendar day is a hard 1,440-minute capacity, even for corrupted legacy data.</summary>
    public static IReadOnlyDictionary<DateOnly, int> GetActualStudyMinutesByLocalDate(
        IEnumerable<StudySession> sessions, Student student, IStudentLocalTime studentLocalTime,
        DateTimeOffset utcNow)
    {
        var secondsByDate = new Dictionary<DateOnly, double>();
        foreach (StudySession session in sessions.Where(x => x.StartedAtUtc is not null))
        {
            DateTimeOffset start = session.StartedAtUtc!.Value;
            DateTimeOffset end = GetEffectiveEnd(session, utcNow);
            if (end <= start) continue;

            foreach ((DateTimeOffset from, DateTimeOffset to) in GetActiveIntervals(session, start, end))
            {
                DateTimeOffset cursor = from;
                while (cursor < to)
                {
                    DateOnly date = studentLocalTime.GetLocalDate(cursor, student);
                    DateTimeOffset next = FindNextLocalDateBoundary(cursor, to, date, student, studentLocalTime);
                    secondsByDate[date] = secondsByDate.GetValueOrDefault(date) + (next - cursor).TotalSeconds;
                    cursor = next;
                }
            }
        }

        return secondsByDate.ToDictionary(
            x => x.Key,
            x => Math.Min(1440, (int)Math.Ceiling(x.Value / 60d)));
    }

    public static int GetActualStudyMinutesForDates(
        IEnumerable<StudySession> sessions, DateOnly startsOn, DateOnly endsOn,
        DateTimeOffset utcNow, Student student, IStudentLocalTime studentLocalTime) =>
        GetActualStudyMinutesByLocalDate(sessions, student, studentLocalTime, utcNow)
            .Where(x => x.Key >= startsOn && x.Key <= endsOn)
            .Sum(x => x.Value);

    public static ParentDashboardSessionsCsvExport CreateCsvExport(
        Guid studentId,
        IEnumerable<ParentDashboardStudySessionResponse> sessions,
        DateTimeOffset utcNow)
    {
        var builder = new StringBuilder();
        AppendCsvRow(
            builder,
            [
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
                    FormatDateTimeOffset(session.StartedAtUtc),
                    session.Status,
                    session.Mode,
                    session.SelectedSubjectId?.ToString() ?? NullCsvValue,
                    session.SubjectName is null ? NullCsvValue : SanitizeCsvTextValue(session.SubjectName),
                    session.ActualStudyMinutes,
                    session.PlannedFocusDurationMinutes ?? NullCsvValue,
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

    // CSV is an analytic export, deliberately retaining integer-minute values
    // even though the interactive Parent Overview presents friendly strings.
    public static ParentDashboardSessionsCsvExport CreateRawCsvExport(
        Guid studentId,
        IEnumerable<StudySession> sessions,
        IReadOnlyDictionary<Guid, string?> subjectNamesById,
        DateTimeOffset utcNow)
    {
        var builder = new StringBuilder();
        AppendCsvRow(builder,
        ["StartedAtUtc", "Status", "Mode", "SelectedSubjectId", "SubjectName",
            "ActualStudyMinutes", "PlannedFocusDurationMinutes", "CompletedAtUtc",
            "CancelledAtUtc", "LastActivityAtUtc"]);

        foreach (StudySession session in sessions)
        {
            string? subjectName = session.SelectedSubjectId is Guid subjectId &&
                                  subjectNamesById.TryGetValue(subjectId, out string? name)
                ? name : null;
            AppendCsvRow(builder,
            [
                FormatDateTimeOffset(session.StartedAtUtc), session.Status.ToString(),
                session.Mode.ToString(), session.SelectedSubjectId?.ToString() ?? NullCsvValue,
                subjectName is null ? NullCsvValue : SanitizeCsvTextValue(subjectName),
                GetActualStudyMinutes(session, utcNow).ToString(CultureInfo.InvariantCulture),
                session.FocusDurationMinutes?.ToString(CultureInfo.InvariantCulture) ?? NullCsvValue,
                FormatDateTimeOffset(session.CompletedAtUtc), FormatDateTimeOffset(session.CancelledAtUtc),
                FormatDateTimeOffset(session.LastActivityAtUtc)
            ]);
        }

        string timestamp = utcNow.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return new ParentDashboardSessionsCsvExport(
            $"parent-dashboard-sessions-{studentId:N}-{timestamp}.csv", CsvContentType,
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

        // Do not turn a stale Active row into arbitrary wall-clock study time.
        // LastActivity is advanced only by a real progress/heartbeat request.
        return session.Status == StudySessionStatus.Active && session.LastActivityAtUtc is not null
            ? session.LastActivityAtUtc.Value < utcNow ? session.LastActivityAtUtc.Value : utcNow
            : utcNow;
    }

    private static IEnumerable<(DateTimeOffset From, DateTimeOffset To)> GetActiveIntervals(
        StudySession session, DateTimeOffset start, DateTimeOffset end)
    {
        DateTimeOffset cursor = start;
        foreach (StudySessionPauseInterval pause in session.PauseIntervals.OrderBy(x => x.StartedAtUtc))
        {
            DateTimeOffset pauseStart = pause.StartedAtUtc < start ? start : pause.StartedAtUtc;
            DateTimeOffset pauseEnd = (pause.EndedAtUtc ?? end) > end ? end : pause.EndedAtUtc ?? end;
            if (pauseStart > cursor) yield return (cursor, pauseStart);
            if (pauseEnd > cursor) cursor = pauseEnd;
        }
        if (cursor < end) yield return (cursor, end);
    }

    private static DateTimeOffset FindNextLocalDateBoundary(DateTimeOffset from, DateTimeOffset end,
        DateOnly currentDate, Student student, IStudentLocalTime studentLocalTime)
    {
        if (studentLocalTime.GetLocalDate(end, student) == currentDate) return end;
        long low = from.UtcTicks;
        long high = end.UtcTicks;
        while (high - low > TimeSpan.TicksPerSecond)
        {
            long middle = low + (high - low) / 2;
            DateTimeOffset probe = new(middle, TimeSpan.Zero);
            if (studentLocalTime.GetLocalDate(probe, student) == currentDate) low = middle;
            else high = middle;
        }
        return new DateTimeOffset(high, TimeSpan.Zero);
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
        value?.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture) ?? NullCsvValue;
}

internal sealed record ParentDashboardContext(Parent Parent, Student Student, ParentStudentRelationship Relationship);

internal sealed record ParentDashboardSessionFilters(
    IReadOnlyCollection<StudySessionStatus> Statuses,
    Guid? SubjectId);
