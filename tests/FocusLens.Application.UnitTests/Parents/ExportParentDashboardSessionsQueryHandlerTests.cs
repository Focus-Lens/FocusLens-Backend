using System.Text;
using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class ExportParentDashboardSessionsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Export_UsesRequiredColumnsQuotedDateTimesAndNullLiterals()
    {
        TestContext context = CreateContext();
        StudySession completed = ReadySession(context.Student.Id, 30);
        Assert.True(completed.Start(new DateTimeOffset(2026, 10, 2, 20, 0, 0, TimeSpan.Zero)).IsSuccess);
        Assert.True(completed.UpdateProgress(1, new DateTimeOffset(2026, 10, 2, 20, 15, 0, TimeSpan.Zero)).IsSuccess);
        Assert.True(completed.CompleteSuccessfully(new DateTimeOffset(2026, 10, 2, 20, 30, 0, TimeSpan.Zero))
            .IsSuccess);

        StudySession active = ReadySession(context.Student.Id, 30);
        Assert.True(active.Start(new DateTimeOffset(2026, 10, 2, 19, 0, 0, TimeSpan.Zero)).IsSuccess);
        Assert.True(active.UpdateProgress(1, new DateTimeOffset(2026, 10, 2, 19, 15, 0, TimeSpan.Zero)).IsSuccess);
        completed.SetPrivateProperty("SelectedSubjectId", null!);
        active.SetPrivateProperty("SelectedSubjectId", null!);

        Result<ParentDashboardSessionsCsvExport> result = await CreateHandler(context, [completed, active])
            .Handle(new ExportParentDashboardSessionsQuery(context.Student.Id, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        string[] rows = Encoding.UTF8.GetString(result.Value.Content)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        string[] expectedHeader =
        [
            "StartedAtUtc", "Status", "Mode", "SelectedSubjectId", "SubjectName",
            "ActualStudyMinutes", "PlannedFocusDurationMinutes", "CompletedAtUtc",
            "CancelledAtUtc", "LastActivityAtUtc"
        ];
        Assert.Equal(expectedHeader, ParseCsvRow(rows[0]));
        Assert.DoesNotContain("Id", ParseCsvRow(rows[0]));
        Assert.DoesNotContain(completed.Id.ToString(), rows[1]);
        Assert.DoesNotContain(active.Id.ToString(), rows[2]);

        Assert.Contains("\"Oct 02, 2026 08:00 PM\"", rows[1]);
        Assert.Contains("\"Oct 02, 2026 08:30 PM\"", rows[1]);

        string[][] values = rows.Skip(1).Select(ParseCsvRow).ToArray();
        Assert.All(values, row => Assert.Equal(expectedHeader.Length, row.Length));

        Assert.Equal(
        [
            "Oct 02, 2026 08:00 PM", "Completed", "Digital", "NULL", "NULL", "30", "30",
            "Oct 02, 2026 08:30 PM", "NULL", "Oct 02, 2026 08:15 PM"
        ], values[0]);
        Assert.Equal(
        [
            "Oct 02, 2026 07:00 PM", "Active", "Digital", "NULL", "NULL", "15", "30",
            "NULL", "NULL", "Oct 02, 2026 07:15 PM"
        ], values[1]);
    }

    private static ExportParentDashboardSessionsQueryHandler CreateHandler(
        TestContext context,
        IEnumerable<StudySession> sessions) =>
        new(
            new InMemoryRepository<Parent>(context.Parent),
            new InMemoryRepository<ParentStudentRelationship>(context.Relationship),
            new InMemoryRepository<Student>(context.Student),
            new InMemoryRepository<StudySession>(sessions.ToArray()),
            new InMemoryRepository<StudentWeek>(new StudentWeek(context.Student.Id, new DateOnly(2026, 9, 26))),
            new FakeCurrentUser(context.Parent.UserId),
            new FixedTimeProvider(Now));

    private static TestContext CreateContext()
    {
        Parent parent = new(Guid.NewGuid());
        Student student = new(Guid.NewGuid());
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        student.SetWeekStartsOn(DayOfWeek.Saturday);
        student.SetParentSharingPreferences(true, true);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        return new TestContext(parent, student, relationship);
    }

    private static StudySession ReadySession(Guid studentId, int minutes)
    {
        StudySession session = StudySession.Create(studentId, StudySessionMode.Digital).Value;
        StudyMaterial material = StudyMaterial.Create(
            studentId, "book.pdf", 1, 1, $"materials/{Guid.NewGuid()}/book.pdf", StudyMaterialSource.Upload).Value;
        Assert.True(session.SetSubjectId(Guid.NewGuid()).IsSuccess);
        Assert.True(session.SetDuration(minutes).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.SetSelection(StudySessionSelection.Create(session, material, 1, 1).Value).IsSuccess);
        Assert.True(session.SetSelectedSections([]).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        return session;
    }

    private static string[] ParseCsvRow(string row)
    {
        List<string> values = [];
        StringBuilder value = new();
        bool isQuoted = false;

        for (int index = 0; index < row.Length; index++)
        {
            char character = row[index];
            if (character == '"')
            {
                if (isQuoted && index + 1 < row.Length && row[index + 1] == '"')
                {
                    value.Append(character);
                    index++;
                }
                else
                {
                    isQuoted = !isQuoted;
                }
            }
            else if (character == ',' && !isQuoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        values.Add(value.ToString());
        return [.. values];
    }

    private sealed record TestContext(Parent Parent, Student Student, ParentStudentRelationship Relationship);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}