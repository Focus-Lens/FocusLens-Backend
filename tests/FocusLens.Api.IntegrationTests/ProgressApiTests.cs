using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using FocusLens.Domain.Identity;

namespace FocusLens.Api.IntegrationTests;

public sealed class ProgressApiTests
{
    [Fact]
    public async Task StudentProgress_CustomRange_AggregatesSubjectsAndReturnsEnoughData()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid userId = Guid.NewGuid();
        Student student = CreateStudent(userId, out StudentSubject math, out StudentSubject biology);
        StudySession first = CreateHistoricalSession(student, math, At(2026, 9, 1), StudySessionStatus.Paused);
        StudySession second = CreateHistoricalSession(student, math, At(2026, 9, 2), StudySessionStatus.Completed);
        StudySession third = CreateHistoricalSession(student, biology, At(2026, 9, 2), StudySessionStatus.Cancelled);

        await factory.SeedAsync(db =>
        {
            db.Users.Add(new ApplicationUser { Id = userId, FirstName = "Test", LastName = "Student" });
            db.Students.Add(student);
            db.StudySessions.AddRange(first, second, third);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");
        HttpResponseMessage response = await client.GetAsync(
            "/api/progress?range=Custom&dateFrom=2026-09-01&dateTo=2026-09-02");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Assert.Equal(3, root.GetProperty("sessionCount").GetInt32());
        Assert.Equal(126, root.GetProperty("actualStudyMinutes").GetInt32());
        Assert.Equal("EnoughData", root.GetProperty("dataStatus").GetString());
        Assert.Equal(0, root.GetProperty("learningPercentage").GetInt32());
        JsonElement daily = root.GetProperty("daily");
        Assert.Equal(2, daily.GetArrayLength());
        Assert.Equal(1, daily[0].GetProperty("sessionCount").GetInt32());
        Assert.Equal(2, daily[1].GetProperty("sessionCount").GetInt32());
        JsonElement mathProgress = root.GetProperty("subjects").EnumerateArray()
            .Single(item => item.GetProperty("subjectId").GetGuid() == math.Id);
        Assert.Equal(2, mathProgress.GetProperty("sessionCount").GetInt32());
        Assert.Equal(84, mathProgress.GetProperty("actualStudyMinutes").GetInt32());
        Assert.Equal(0, mathProgress.GetProperty("learningPercentage").GetInt32());
    }

    [Fact]
    public async Task ParentProgress_RequiresActiveRelationship_AndReturnsInsufficientDataForOneSession()
    {
        await using CustomWebApplicationFactory factory = new();
        Student child = CreateStudent(Guid.NewGuid(), out StudentSubject math, out _);
        StudySession session = CreateHistoricalSession(child, math, At(2026, 9, 1), StudySessionStatus.Completed);
        child.SetParentSharingPreferences(false, true);
        Student unlinkedChild = CreateStudent(Guid.NewGuid(), out _, out _);
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ParentStudentRelationship relationship = new(parent.Id, child.Id);
        relationship.Accept(At(2026, 8, 1));

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new ApplicationUser { Id = parentUserId, FirstName = "Test", LastName = "Parent" },
                new ApplicationUser { Id = child.UserId, FirstName = "Test", LastName = "Student" },
                new ApplicationUser { Id = unlinkedChild.UserId, FirstName = "Test", LastName = "Student" });
            db.Students.AddRange(child, unlinkedChild);
            db.Parents.Add(parent);
            db.ParentStudentRelationships.Add(relationship);
            db.StudySessions.Add(session);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");
        HttpResponseMessage linked = await client.GetAsync(
            $"/api/progress?studentId={child.Id}&range=Custom&dateFrom=2026-09-01&dateTo=2026-09-01");
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await linked.Content.ReadAsStringAsync());
        Assert.Equal("InsufficientData", document.RootElement.GetProperty("dataStatus").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            $"/api/progress?studentId={unlinkedChild.Id}&range=Last7Days")).StatusCode);
    }

    [Fact]
    public async Task Progress_UsesWeightedLearningAndAggregatesQuestionDataBySubject()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid userId = Guid.NewGuid();
        Student student = CreateStudent(userId, out StudentSubject math, out _);
        StudySession first = CreateHistoricalSession(student, math, At(2026, 9, 1), StudySessionStatus.Completed);
        StudySession second = CreateHistoricalSession(student, math, At(2026, 9, 2), StudySessionStatus.Completed);
        StudySessionQuestion oneQuestion = CreateQuestion(first.Id, 1);
        StudySessionQuestion secondQuestion = CreateQuestion(second.Id, 1);
        StudySessionQuestion thirdQuestion = CreateQuestion(second.Id, 2);

        await factory.SeedAsync(db =>
        {
            db.Users.Add(new ApplicationUser { Id = userId, FirstName = "Test", LastName = "Student" });
            db.Students.Add(student);
            db.StudySessions.AddRange(first, second);
            db.StudySessionQuestions.AddRange(oneQuestion, secondQuestion, thirdQuestion);
            db.StudySessionQuestionAnswers.AddRange(
                CreateAnswer(oneQuestion.Id, true, 1),
                CreateAnswer(secondQuestion.Id, true, 1));
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");
        HttpResponseMessage response = await client.GetAsync(
            "/api/progress?range=Custom&dateFrom=2026-09-01&dateTo=2026-09-02");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(67, document.RootElement.GetProperty("learningPercentage").GetInt32());
        JsonElement subject = Assert.Single(document.RootElement.GetProperty("subjects").EnumerateArray());
        Assert.Equal(67, subject.GetProperty("learningPercentage").GetInt32());
    }

    [Fact]
    public async Task Progress_LastSevenAndThirtyDays_AndEmptyRangeHaveExplicitSemantics()
    {
        DateTimeOffset now = At(2026, 9, 14);
        await using CustomWebApplicationFactory factory = new(now);
        Guid userId = Guid.NewGuid();
        Student student = CreateStudent(userId, out StudentSubject math, out _);
        StudySession recent = CreateHistoricalSession(student, math, now.AddDays(-1), StudySessionStatus.Paused);
        StudySession older = CreateHistoricalSession(student, math, now.AddDays(-8), StudySessionStatus.Completed);

        await factory.SeedAsync(db =>
        {
            db.Users.Add(new ApplicationUser { Id = userId, FirstName = "Test", LastName = "Student" });
            db.Students.Add(student);
            db.StudySessions.AddRange(recent, older);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");
        using JsonDocument lastSeven = JsonDocument.Parse(await (await client.GetAsync("/api/progress?range=Last7Days"))
            .Content.ReadAsStringAsync());
        using JsonDocument lastThirty = JsonDocument.Parse(await (await client.GetAsync("/api/progress?range=Last30Days"))
            .Content.ReadAsStringAsync());
        using JsonDocument empty = JsonDocument.Parse(await (await client.GetAsync(
            "/api/progress?range=Custom&dateFrom=2026-01-01&dateTo=2026-01-01")).Content.ReadAsStringAsync());

        Assert.Equal(1, lastSeven.RootElement.GetProperty("sessionCount").GetInt32());
        Assert.Equal(2, lastThirty.RootElement.GetProperty("sessionCount").GetInt32());
        Assert.Equal(0, empty.RootElement.GetProperty("sessionCount").GetInt32());
        Assert.Equal("InsufficientData", empty.RootElement.GetProperty("dataStatus").GetString());
    }

    [Fact]
    public async Task Progress_RejectsInvalidCustomRange_AndBucketsBoundarySessionsByUtcDate()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid userId = Guid.NewGuid();
        Student student = CreateStudent(userId, out StudentSubject math, out _);
        StudySession boundary = CreateHistoricalSession(student, math,
            new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.Zero), StudySessionStatus.Completed);
        await factory.SeedAsync(db =>
        {
            db.Users.Add(new ApplicationUser { Id = userId, FirstName = "Test", LastName = "Student" });
            db.Students.Add(student);
            db.StudySessions.Add(boundary);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(
            "/api/progress?range=Custom&dateFrom=2026-09-02&dateTo=2026-09-01")).StatusCode);

        HttpResponseMessage response = await client.GetAsync(
            "/api/progress?range=Custom&dateFrom=2026-09-01&dateTo=2026-09-02");
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement daily = document.RootElement.GetProperty("daily");
        Assert.Equal(1, daily[0].GetProperty("sessionCount").GetInt32());
        Assert.Equal(0, daily[1].GetProperty("sessionCount").GetInt32());
    }

    private static Student CreateStudent(Guid userId, out StudentSubject math, out StudentSubject biology)
    {
        math = StudentSubject.Predefined(StudentSubjectType.Math);
        biology = StudentSubject.Predefined(StudentSubjectType.Biology);
        Student student = new(userId);
        student.ReplaceSubjects([math, biology]);
        return student;
    }

    private static StudySession CreateHistoricalSession(Student student, StudentSubject subject, DateTimeOffset startedAt,
        StudySessionStatus finalStatus)
    {
        StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
        StudyMaterial material = StudyMaterial.Create(student.Id, "material.pdf", 1, 1,
            $"materials/{session.Id}.pdf", StudyMaterialSource.Upload).Value;
        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(30).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        Assert.True(session.Start(startedAt).IsSuccess);
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

    private static StudySessionQuestion CreateQuestion(Guid sessionId, int order) =>
        StudySessionQuestion.Create(sessionId, null, $"section-{order}", "Section", $"concept-{order}", "Concept",
            "Question?", ["A", "B", "C", "D"], "A", "easy", "Explanation", 1, order).Value;

    private static StudySessionQuestionAnswer CreateAnswer(Guid questionId, bool correct, int attempt) =>
        StudySessionQuestionAnswer.Create(questionId, correct ? "A" : "B", correct, null, attempt,
            At(2026, 9, 3).AddMinutes(attempt)).Value;

    private static DateTimeOffset At(int year, int month, int day) =>
        new(year, month, day, 10, 0, 0, TimeSpan.Zero);

    private static HttpClient CreateClient(CustomWebApplicationFactory factory, Guid userId, string role)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(userId, role));
        return client;
    }
}
