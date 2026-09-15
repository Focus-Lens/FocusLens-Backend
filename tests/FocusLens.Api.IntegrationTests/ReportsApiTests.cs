using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Api.IntegrationTests;

public sealed class ReportsApiTests
{
    [Fact]
    public async Task StudentReports_ExcludeMutableSessions_AndCalculateRetriesAndPausedDuration()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid userId = Guid.NewGuid();
        Student student = CreateStudent(userId, out StudentSubject math, out _);
        DateTimeOffset start = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
        StudySession paused = CreateHistoricalSession(student, math, start, StudySessionStatus.Paused);
        StudySession completed = CreateHistoricalSession(student, math, start.AddDays(-1), StudySessionStatus.Completed);
        StudySession cancelled = CreateHistoricalSession(student, math, start.AddDays(-2), StudySessionStatus.Cancelled);
        StudySession active = CreateActiveSession(student, math, start.AddDays(-3));
        StudySession draft = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
        StudySessionQuestion question = CreateQuestion(paused.Id);
        StudySessionQuestionAnswer[] answers =
        [
            CreateAnswer(question.Id, "B", false, 1),
            CreateAnswer(question.Id, "C", false, 2),
            CreateAnswer(question.Id, "A", true, 3)
        ];

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudySessions.AddRange(paused, completed, cancelled, active, draft);
            db.StudySessionQuestions.Add(question);
            db.StudySessionQuestionAnswers.AddRange(answers);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");
        HttpResponseMessage response = await client.GetAsync("/api/reports/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement items = document.RootElement.GetProperty("items");
        Assert.Equal(3, items.GetArrayLength());
        Assert.DoesNotContain(items.EnumerateArray(), item => item.GetProperty("sessionId").GetGuid() == active.Id);
        JsonElement pausedItem = items.EnumerateArray().Single(item => item.GetProperty("sessionId").GetGuid() == paused.Id);
        Assert.Equal(42, pausedItem.GetProperty("actualDurationMinutes").GetInt32());
        Assert.Equal(1, pausedItem.GetProperty("questionsGenerated").GetInt32());
        Assert.Equal(1, pausedItem.GetProperty("correctQuestions").GetInt32());
        Assert.Equal(3, pausedItem.GetProperty("attempts").GetInt32());
        Assert.Equal(100, pausedItem.GetProperty("learningPercentage").GetInt32());

        HttpResponseMessage activeDetail = await client.GetAsync($"/api/reports/sessions/{active.Id}");
        Assert.Equal(HttpStatusCode.NotFound, activeDetail.StatusCode);
    }

    [Fact]
    public async Task StudentReportFilters_AndPagination_UseHistoricalSessionsOnly()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid userId = Guid.NewGuid();
        Student student = CreateStudent(userId, out StudentSubject math, out StudentSubject biology);
        StudySession mathFirst = CreateHistoricalSession(student, math,
            new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero), StudySessionStatus.Completed);
        StudySession biologySession = CreateHistoricalSession(student, biology,
            new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero), StudySessionStatus.Paused);
        StudySession mathLatest = CreateHistoricalSession(student, math,
            new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero), StudySessionStatus.Cancelled);

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudySessions.AddRange(mathFirst, biologySession, mathLatest);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");
        HttpResponseMessage filtered = await client.GetAsync(
            $"/api/reports/sessions?dateFrom=2026-09-02&dateTo=2026-09-03&subjectId={math.Id}&status=Cancelled&page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        using JsonDocument filteredDocument = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        JsonElement filteredItems = filteredDocument.RootElement.GetProperty("items");
        Assert.Single(filteredItems.EnumerateArray());
        Assert.Equal(mathLatest.Id, filteredItems[0].GetProperty("sessionId").GetGuid());
        Assert.Equal(1, filteredDocument.RootElement.GetProperty("totalCount").GetInt32());

        HttpResponseMessage paged = await client.GetAsync("/api/reports/sessions?page=2&pageSize=1");
        using JsonDocument pagedDocument = JsonDocument.Parse(await paged.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, paged.StatusCode);
        Assert.Equal(3, pagedDocument.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(biologySession.Id, pagedDocument.RootElement.GetProperty("items")[0].GetProperty("sessionId").GetGuid());
    }

    [Fact]
    public async Task ParentReports_RequireAnActiveRelationship_AndStudentCannotRequestAnotherStudent()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid childUserId = Guid.NewGuid();
        Student child = CreateStudent(childUserId, out StudentSubject math, out _);
        StudySession childSession = CreateHistoricalSession(child, math,
            new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero), StudySessionStatus.Completed);
        Guid otherUserId = Guid.NewGuid();
        Student other = CreateStudent(otherUserId, out _, out _);
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ParentStudentRelationship relationship = new(parent.Id, child.Id);
        relationship.Accept(new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));

        await factory.SeedAsync(db =>
        {
            db.Students.AddRange(child, other);
            db.Parents.Add(parent);
            db.ParentStudentRelationships.Add(relationship);
            db.StudySessions.Add(childSession);
            return Task.CompletedTask;
        });

        using HttpClient parentClient = CreateClient(factory, parentUserId, "Parent");
        Assert.Equal(HttpStatusCode.OK,
            (await parentClient.GetAsync($"/api/reports/sessions?studentId={child.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await parentClient.GetAsync($"/api/reports/sessions?studentId={other.Id}")).StatusCode);

        using HttpClient studentClient = CreateClient(factory, childUserId, "Student");
        Assert.Equal(HttpStatusCode.NotFound,
            (await studentClient.GetAsync($"/api/reports/sessions?studentId={other.Id}")).StatusCode);
    }

    private static Student CreateStudent(Guid userId, out StudentSubject math, out StudentSubject biology)
    {
        math = StudentSubject.Predefined(StudentSubjectType.Math);
        biology = StudentSubject.Predefined(StudentSubjectType.Biology);
        Student student = new(userId);
        student.ReplaceSubjects([math, biology]);
        student.SetParentSharingPreferences(true, true);
        return student;
    }

    private static StudySession CreateHistoricalSession(
        Student student,
        StudentSubject subject,
        DateTimeOffset startedAt,
        StudySessionStatus finalStatus)
    {
        StudySession session = CreateActiveSession(student, subject, startedAt);
        var result = finalStatus switch
        {
            StudySessionStatus.Paused => session.Pause(startedAt.AddMinutes(42)),
            StudySessionStatus.Completed => session.CompleteSuccessfully(startedAt.AddMinutes(42)),
            StudySessionStatus.Cancelled => session.End(startedAt.AddMinutes(42)),
            _ => throw new ArgumentOutOfRangeException(nameof(finalStatus))
        };
        Assert.True(result.IsSuccess);
        return session;
    }

    private static StudySession CreateActiveSession(Student student, StudentSubject subject, DateTimeOffset startedAt)
    {
        StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
        StudyMaterial material = StudyMaterial.Create(student.Id, "material.pdf", 1, 1,
            $"materials/{session.Id}.pdf", StudyMaterialSource.Upload).Value;
        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(30).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        Assert.True(session.Start(startedAt).IsSuccess);
        return session;
    }

    private static StudySessionQuestion CreateQuestion(Guid sessionId) =>
        StudySessionQuestion.Create(sessionId, null, "section", "Section", "concept", "Concept", "Question?",
            ["A", "B", "C", "D"], "A", "easy", "Explanation", 2, 1).Value;

    private static StudySessionQuestionAnswer CreateAnswer(Guid questionId, string answer, bool correct, int attempt) =>
        StudySessionQuestionAnswer.Create(questionId, answer, correct, null, attempt,
            new DateTimeOffset(2026, 9, 8, 10, attempt, 0, TimeSpan.Zero)).Value;

    private static HttpClient CreateClient(CustomWebApplicationFactory factory, Guid userId, string role)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(userId, role));
        return client;
    }
}
