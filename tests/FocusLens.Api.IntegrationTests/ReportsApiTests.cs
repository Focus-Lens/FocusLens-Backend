using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
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
        StudySession completed =
            CreateHistoricalSession(student, math, start.AddDays(-1), StudySessionStatus.Completed);
        StudySession cancelled =
            CreateHistoricalSession(student, math, start.AddDays(-2), StudySessionStatus.Cancelled);
        StudySession active = CreateActiveSession(student, math, start.AddDays(-3));
        StudySession draft = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
        StudySessionQuestion question = CreateQuestion(paused.Id);
        StudySessionBehaviorWindow behaviorWindow = StudySessionBehaviorWindow.Create(
            paused.Id,
            1,
            start,
            start.AddMinutes(15),
            false).Value;
        Assert.True(behaviorWindow.RecordAnalysis(82, "Focused", "Improving", null, null, null, null, false).IsSuccess);
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
            db.StudySessionBehaviorWindows.Add(behaviorWindow);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");
        HttpResponseMessage response = await client.GetAsync("/api/reports/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement items = document.RootElement.GetProperty("items");
        Assert.Equal(3, items.GetArrayLength());
        Assert.DoesNotContain(items.EnumerateArray(), item => item.GetProperty("sessionId").GetGuid() == active.Id);
        JsonElement pausedItem =
            items.EnumerateArray().Single(item => item.GetProperty("sessionId").GetGuid() == paused.Id);
        Assert.Equal(42, pausedItem.GetProperty("actualDurationMinutes").GetInt32());
        Assert.Equal(1, pausedItem.GetProperty("questionsGenerated").GetInt32());
        Assert.Equal(1, pausedItem.GetProperty("correctQuestions").GetInt32());
        Assert.Equal(3, pausedItem.GetProperty("attempts").GetInt32());
        Assert.Equal(100, pausedItem.GetProperty("learningPercentage").GetInt32());
        Assert.Equal(82, pausedItem.GetProperty("focusScore").GetInt32());
        Assert.Equal("Focused", pausedItem.GetProperty("focusState").GetString());
        Assert.Equal("STABLE", pausedItem.GetProperty("focusTrend").GetString());

        HttpResponseMessage activeDetail = await client.GetAsync($"/api/reports/sessions/{active.Id}");
        Assert.Equal(HttpStatusCode.NotFound, activeDetail.StatusCode);
    }

    [Fact]
    public async Task ReportSessionDetail_DerivesSessionFocusMetricsFromActiveTimeWeightedWindows()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid userId = Guid.NewGuid();
        Student student = CreateStudent(userId, out StudentSubject math, out _);
        DateTimeOffset start = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
        StudySession session = CreateHistoricalSession(student, math, start, StudySessionStatus.Completed);
        StudySessionBehaviorWindow[] windows =
        [
            CreateBehaviorWindow(session.Id, 1, start, start.AddMinutes(5), 40, "SKIMMING", 60),
            CreateBehaviorWindow(session.Id, 2, start.AddMinutes(5), start.AddMinutes(10), 80, "NORMAL_FOCUSED", 180)
        ];

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudySessions.Add(session);
            db.StudySessionBehaviorWindows.AddRange(windows);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");
        HttpResponseMessage response = await client.GetAsync($"/api/reports/sessions/{session.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Assert.Equal(70, root.GetProperty("focusScore").GetInt32());
        Assert.Equal("NORMAL_FOCUSED", root.GetProperty("focusState").GetString());
        Assert.Equal("IMPROVING", root.GetProperty("focusTrend").GetString());
        Assert.Equal(40, root.GetProperty("focusQuality")[0].GetProperty("focusScore").GetInt32());
        Assert.Equal(80, root.GetProperty("focusQuality")[1].GetProperty("focusScore").GetInt32());

        HttpResponseMessage listResponse = await client.GetAsync("/api/reports/sessions");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using JsonDocument listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        JsonElement listItem = listDocument.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("sessionId").GetGuid() == session.Id);
        Assert.Equal(70, listItem.GetProperty("focusScore").GetInt32());
        Assert.Equal("NORMAL_FOCUSED", listItem.GetProperty("focusState").GetString());
        Assert.Equal("IMPROVING", listItem.GetProperty("focusTrend").GetString());
    }

    [Fact]
    public async Task ReportSessionDetail_ReturnsFocusQualityTimelineFromPersistedWindows()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid userId = Guid.NewGuid();
        Student student = CreateStudent(userId, out StudentSubject math, out _);
        student.SetTimeZoneId("Africa/Cairo");
        DateTimeOffset start = new(2026, 9, 8, 23, 30, 0, TimeSpan.Zero);
        StudySession session = CreateHistoricalSession(student, math, start, StudySessionStatus.Completed);

        StudySessionBehaviorWindow[] windows =
        [
            CreateBehaviorWindow(session.Id, 1, start, start.AddMinutes(7), 45),
            CreateBehaviorWindow(session.Id, 2, start.AddMinutes(7), start.AddMinutes(14), 68),
            CreateBehaviorWindow(session.Id, 3, start.AddMinutes(14), start.AddMinutes(21), 82)
        ];

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudySessions.Add(session);
            db.StudySessionBehaviorWindows.AddRange(windows);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");
        HttpResponseMessage response = await client.GetAsync($"/api/reports/sessions/{session.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement points = document.RootElement.GetProperty("focusQuality");

        Assert.Equal(3, points.GetArrayLength());
        Assert.Equal(1, points[0].GetProperty("windowIndex").GetInt32());
        Assert.Equal(45, points[0].GetProperty("focusScore").GetInt32());
        Assert.Equal(
            new DateTimeOffset(2026, 9, 9, 2, 30, 0, TimeSpan.FromHours(3)),
            points[0].GetProperty("windowStartLocal").GetDateTimeOffset());
        Assert.Equal(
            new DateTimeOffset(2026, 9, 9, 2, 37, 0, TimeSpan.FromHours(3)),
            points[0].GetProperty("windowEndLocal").GetDateTimeOffset());
        Assert.Equal(82, points[2].GetProperty("focusScore").GetInt32());
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
        Assert.Equal(biologySession.Id,
            pagedDocument.RootElement.GetProperty("items")[0].GetProperty("sessionId").GetGuid());
    }

    [Fact]
    public async Task ReportSessionDateFilters_UseStudentLocalCalendarDate()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid userId = Guid.NewGuid();
        Student student = CreateStudent(userId, out StudentSubject math, out _);
        student.SetTimeZoneId("Africa/Cairo");

        StudySession boundarySession = CreateHistoricalSession(
            student,
            math,
            new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.Zero),
            StudySessionStatus.Completed);

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudySessions.Add(boundarySession);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, userId, "Student");

        HttpResponseMessage localDay = await client.GetAsync(
            "/api/reports/sessions?dateFrom=2026-09-02&dateTo=2026-09-02");

        Assert.Equal(HttpStatusCode.OK, localDay.StatusCode);
        using JsonDocument localDayDocument = JsonDocument.Parse(
            await localDay.Content.ReadAsStringAsync());
        Assert.Equal(1, localDayDocument.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(
            boundarySession.Id,
            localDayDocument.RootElement.GetProperty("items")[0].GetProperty("sessionId").GetGuid());

        HttpResponseMessage utcDay = await client.GetAsync(
            "/api/reports/sessions?dateFrom=2026-09-01&dateTo=2026-09-01");

        Assert.Equal(HttpStatusCode.OK, utcDay.StatusCode);
        using JsonDocument utcDayDocument = JsonDocument.Parse(
            await utcDay.Content.ReadAsStringAsync());
        Assert.Equal(0, utcDayDocument.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ParentReports_RequireAnActiveRelationship_AndStudentCannotRequestAnotherStudent()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid childUserId = Guid.NewGuid();
        Student child = CreateStudent(childUserId, out StudentSubject math, out _);
        StudySession childSession = CreateHistoricalSession(child, math,
            new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero), StudySessionStatus.Completed);
        StudySessionBehaviorWindow childWindow = CreateBehaviorWindow(
            childSession.Id, 1, childSession.StartedAtUtc!.Value,
            childSession.StartedAtUtc.Value.AddMinutes(7), 82);
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
            db.StudySessionBehaviorWindows.Add(childWindow);
            return Task.CompletedTask;
        });

        using HttpClient parentClient = CreateClient(factory, parentUserId, "Parent");
        Assert.Equal(HttpStatusCode.OK,
            (await parentClient.GetAsync($"/api/reports/sessions?studentId={child.Id}")).StatusCode);
        using JsonDocument parentDetail = JsonDocument.Parse(
            await (await parentClient.GetAsync(
                    $"/api/reports/sessions/{childSession.Id}?studentId={child.Id}"))
                .Content.ReadAsStringAsync());
        Assert.Equal(1, parentDetail.RootElement.GetProperty("focusQuality").GetArrayLength());
        Assert.Equal(82, parentDetail.RootElement.GetProperty("focusQuality")[0]
            .GetProperty("focusScore").GetInt32());
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
        Result<Success> result = finalStatus switch
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

    private static StudySessionBehaviorWindow CreateBehaviorWindow(
        Guid sessionId,
        int windowIndex,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        int focusScore,
        string focusState = "NORMAL_FOCUSED",
        double? activeTimeSeconds = null)
    {
        StudySessionBehaviorWindow window = StudySessionBehaviorWindow.Create(
            sessionId, windowIndex, windowStart, windowEnd, false).Value;
        Assert.True(window.RecordAnalysis(
            focusScore, focusState, "STABLE", null, null, "CONTINUE", "CONTINUE", false,
            activeTimeSeconds).IsSuccess);
        return window;
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