using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using FocusLens.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Api.IntegrationTests;

public sealed class BehavioralIntelligenceApiTests
{
    [Fact]
    public async Task BehavioralProgress_AggregatesDailyScoresAndDerivesTrendFromEnoughData()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory factory = new(now);
        Guid userId = Guid.NewGuid();
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        Student student = new(userId);
        student.ReplaceSubjects([subject]);
        StudyMaterial material = StudyMaterial.Create(student.Id, "progress.pdf", 1, 1,
            "materials/progress.pdf", StudyMaterialSource.Upload).Value;
        StudySession[] sessions =
        [
            CreateFinishedSession(student, subject, material, now.AddDays(-2)),
            CreateFinishedSession(student, subject, material, now.AddDays(-1)),
            CreateFinishedSession(student, subject, material, now)
        ];
        StudySessionBehaviorWindow[] windows = sessions.Select((session, index) =>
        {
            DateTimeOffset end = now.AddDays(index - 2);
            StudySessionBehaviorWindow window = StudySessionBehaviorWindow.Create(
                session.Id, 1, end.AddMinutes(-5), end, false).Value;
            Assert.True(window.RecordAnalysis(60 + (index * 10), "NORMAL_FOCUSED", "STABLE",
                70 + (index * 5), "STABLE", "CONTINUE", "CONTINUE", false).IsSuccess);
            return window;
        }).ToArray();
        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudyMaterials.Add(material);
            db.StudySessions.AddRange(sessions);
            db.StudySessionBehaviorWindows.AddRange(windows);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(userId, "Student"));
        HttpResponseMessage response = await client.GetAsync("/api/progress/behavioral?range=Week");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Assert.Equal("EnoughData", root.GetProperty("dataStatus").GetString());
        Assert.Equal(80, root.GetProperty("focus").GetProperty("latestScore").GetInt32());
        Assert.Equal("Improving", root.GetProperty("focus").GetProperty("trend").GetString());
        Assert.Equal(80, root.GetProperty("understanding").GetProperty("latestScore").GetInt32());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.OK)]
    public async Task BehavioralProgress_ParentAccess_RequiresSubjectTrendSharing(
        bool shareSubjectTrends,
        HttpStatusCode expectedStatus)
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory factory = new(now);

        Guid childUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        Student child = new(childUserId);
        child.ReplaceSubjects([subject]);
        child.SetParentSharingPreferences(
            shareSessionSummaries: true,
            shareSubjectTrends: shareSubjectTrends);

        StudyMaterial material = StudyMaterial.Create(
            child.Id, "behavioral-progress.pdf", 1, 1,
            "materials/behavioral-progress.pdf", StudyMaterialSource.Upload).Value;
        StudySession session = CreateFinishedSession(child, subject, material, now.AddMinutes(-10));
        StudySessionBehaviorWindow window = StudySessionBehaviorWindow.Create(
            session.Id, 1, now.AddMinutes(-5), now, false).Value;
        Assert.True(window.RecordAnalysis(
            75,
            "NORMAL_FOCUSED",
            "STABLE",
            80,
            "STABLE",
            "CONTINUE",
            "CONTINUE",
            false).IsSuccess);

        Parent parent = new(parentUserId);
        ParentStudentRelationship relationship = new(parent.Id, child.Id);
        relationship.Accept(now.AddDays(-1));

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new ApplicationUser { Id = parentUserId, FirstName = "Test", LastName = "Parent" },
                new ApplicationUser { Id = childUserId, FirstName = "Test", LastName = "Student" });
            db.Students.Add(child);
            db.Parents.Add(parent);
            db.ParentStudentRelationships.Add(relationship);
            db.StudyMaterials.Add(material);
            db.StudySessions.Add(session);
            db.StudySessionBehaviorWindows.Add(window);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(parentUserId, "Parent"));

        HttpResponseMessage response = await client.GetAsync(
            $"/api/progress/behavioral?studentId={child.Id}&range=Week");

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus != HttpStatusCode.OK)
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            75,
            document.RootElement.GetProperty("focus").GetProperty("latestScore").GetInt32());
    }

    [Fact]
    public async Task BehavioralProgress_UsesStudentLocalDatesAndActiveTimeWeightedFocusScores()
    {
        DateTimeOffset now = new(2026, 9, 15, 0, 30, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory factory = new(now);
        Guid userId = Guid.NewGuid();
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        Student student = new(userId);
        student.ReplaceSubjects([subject]);
        student.SetTimeZoneId("Africa/Cairo");

        StudyMaterial material = StudyMaterial
            .Create(
                student.Id,
                "local-progress.pdf",
                1,
                1,
                "materials/local-progress.pdf",
                StudyMaterialSource.Upload
            )
            .Value;
        StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(240).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        Assert.True(
            session.Start(new DateTimeOffset(2026, 9, 14, 20, 0, 0, TimeSpan.Zero)).IsSuccess
        );
        Assert.True(session.End(now.AddMinutes(-10)).IsSuccess);

        StudySessionBehaviorWindow first = StudySessionBehaviorWindow
            .Create(
                session.Id,
                1,
                new DateTimeOffset(2026, 9, 14, 21, 25, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 14, 21, 30, 0, TimeSpan.Zero),
                false
            )
            .Value;
        Assert.True(
            first
                .RecordAnalysis(
                    40,
                    "NORMAL_FOCUSED",
                    "STABLE",
                    70,
                    "STABLE",
                    "CONTINUE",
                    "CONTINUE",
                    false,
                    windowActiveTimeSeconds: 180
                )
                .IsSuccess
        );

        StudySessionBehaviorWindow second = StudySessionBehaviorWindow
            .Create(
                session.Id,
                2,
                new DateTimeOffset(2026, 9, 14, 22, 25, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 14, 22, 30, 0, TimeSpan.Zero),
                false
            )
            .Value;
        Assert.True(
            second
                .RecordAnalysis(
                    80,
                    "NORMAL_FOCUSED",
                    "STABLE",
                    90,
                    "STABLE",
                    "CONTINUE",
                    "CONTINUE",
                    false,
                    windowActiveTimeSeconds: 60
                )
                .IsSuccess
        );

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudyMaterials.Add(material);
            db.StudySessions.Add(session);
            db.StudySessionBehaviorWindows.AddRange(first, second);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create(userId, "Student")
        );
        HttpResponseMessage response = await client.GetAsync("/api/progress/behavioral?range=Week");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );
        JsonElement root = document.RootElement;
        Assert.Equal("2026-09-15", root.GetProperty("dateTo").GetString());

        JsonElement scoredDay = Assert.Single(
            root.GetProperty("daily").EnumerateArray(),
            item => item.GetProperty("focusScore").ValueKind != JsonValueKind.Null
        );
        Assert.Equal("2026-09-15", scoredDay.GetProperty("date").GetString());
        Assert.Equal(2, scoredDay.GetProperty("analyzedWindowCount").GetInt32());
        Assert.Equal(50, scoredDay.GetProperty("focusScore").GetInt32());
        Assert.Equal(80, scoredDay.GetProperty("understandingScore").GetInt32());
        Assert.Equal(50, root.GetProperty("focus").GetProperty("latestScore").GetInt32());
        Assert.Contains(
            "local calendar date",
            root.GetProperty("focus").GetProperty("calculation").GetString()
        );
    }

    [Fact]
    public async Task BehavioralProgress_UsesSameDateRangeContractAsProgress()
    {
        DateTimeOffset now = new(2026, 9, 15, 0, 30, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory factory = new(now);
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        student.SetTimeZoneId("Africa/Cairo");

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(userId, "Student"));

        string[] ranges =
        [
            "/api/progress?range=Last7Days",
            "/api/progress?range=Last30Days",
            "/api/progress?range=Custom&dateFrom=2026-09-01&dateTo=2026-09-10"
        ];
        string[] behavioralRanges =
        [
            "/api/progress/behavioral?range=Last7Days",
            "/api/progress/behavioral?range=Last30Days",
            "/api/progress/behavioral?range=Custom&dateFrom=2026-09-01&dateTo=2026-09-10"
        ];

        for (int index = 0; index < ranges.Length; index++)
        {
            using HttpResponseMessage progressResponse = await client.GetAsync(ranges[index]);
            using HttpResponseMessage behavioralResponse = await client.GetAsync(behavioralRanges[index]);
            Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, behavioralResponse.StatusCode);

            using JsonDocument progressDocument = JsonDocument.Parse(
                await progressResponse.Content.ReadAsStringAsync());
            using JsonDocument behavioralDocument = JsonDocument.Parse(
                await behavioralResponse.Content.ReadAsStringAsync());
            Assert.Equal(
                progressDocument.RootElement.GetProperty("dateFrom").GetString(),
                behavioralDocument.RootElement.GetProperty("dateFrom").GetString());
            Assert.Equal(
                progressDocument.RootElement.GetProperty("dateTo").GetString(),
                behavioralDocument.RootElement.GetProperty("dateTo").GetString());
        }

        const string invalidRange = "?range=Custom&dateFrom=2026-09-10&dateTo=2026-09-01";
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/progress{invalidRange}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/progress/behavioral{invalidRange}")).StatusCode);
    }

    [Fact]
    public async Task BehavioralProgress_IncludesInRangeWindowsFromSessionsStartedBeforeRange()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory factory = new(now);
        Guid userId = Guid.NewGuid();
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        Student student = new(userId);
        student.ReplaceSubjects([subject]);
        student.SetTimeZoneId("Africa/Cairo");
        StudyMaterial material = StudyMaterial.Create(
            student.Id, "cross-date-progress.pdf", 1, 1,
            "materials/cross-date-progress.pdf", StudyMaterialSource.Upload).Value;

        // The session starts at 23:58 in Cairo on Aug 31, but its behavior window
        // ends at 00:02 on Sep 1, inside the selected date range.
        DateTimeOffset startedAt = new(2026, 8, 31, 20, 58, 0, TimeSpan.Zero);
        StudySession session = CreateFinishedSession(student, subject, material, startedAt);
        StudySessionBehaviorWindow window = CreateAnalyzedWindow(
            session.Id, new DateTimeOffset(2026, 8, 31, 21, 2, 0, TimeSpan.Zero), 75);

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudyMaterials.Add(material);
            db.StudySessions.Add(session);
            db.StudySessionBehaviorWindows.Add(window);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(userId, "Student"));
        using HttpResponseMessage response = await client.GetAsync(
            "/api/progress/behavioral?range=Custom&dateFrom=2026-09-01&dateTo=2026-09-10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        JsonElement dailyItem = Assert.Single(root.GetProperty("daily").EnumerateArray(),
            item => item.GetProperty("date").GetString() == "2026-09-01");
        Assert.Equal(1, dailyItem.GetProperty("analyzedWindowCount").GetInt32());
        Assert.Equal(75, dailyItem.GetProperty("focusScore").GetInt32());
        Assert.Equal(75, dailyItem.GetProperty("understandingScore").GetInt32());

        // Session-level subject insights intentionally remain scoped by session start date.
        Assert.Empty(root.GetProperty("subjects").EnumerateArray());

        JsonElement evening = Assert.Single(root.GetProperty("timeOfDay").EnumerateArray(),
            item => item.GetProperty("period").GetString() == "Evening");
        Assert.Equal(1, evening.GetProperty("analyzedWindowCount").GetInt32());
        Assert.Equal(75, evening.GetProperty("averageFocusScore").GetInt32());
    }

    [Fact]
    public async Task BehavioralProgress_ReturnsSubjectFocusTrendsAndLocalTimeOfDayInsights()
    {
        DateTimeOffset now = new(2026, 9, 15, 0, 30, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory factory = new(now);
        Guid userId = Guid.NewGuid();
        StudentSubject math = StudentSubject.Predefined(StudentSubjectType.Math);
        StudentSubject biology = StudentSubject.Predefined(StudentSubjectType.Biology);
        StudentSubject english = StudentSubject.Predefined(StudentSubjectType.English);
        Student student = new(userId);
        student.ReplaceSubjects([math, biology, english]);
        student.SetTimeZoneId("Africa/Cairo");
        StudyMaterial material = StudyMaterial.Create(
            student.Id, "progress-insights.pdf", 1, 1,
            "materials/progress-insights.pdf", StudyMaterialSource.Upload).Value;

        List<StudySession> sessions = [];
        List<StudySessionBehaviorWindow> windows = [];
        (DateTimeOffset WindowEnd, int Score)[] mathWindows =
        [
            (new DateTimeOffset(2026, 9, 12, 13, 0, 0, TimeSpan.Zero), 60),
            (new DateTimeOffset(2026, 9, 13, 13, 0, 0, TimeSpan.Zero), 70),
            (new DateTimeOffset(2026, 9, 14, 13, 0, 0, TimeSpan.Zero), 80)
        ];
        (DateTimeOffset WindowEnd, int Score)[] biologyWindows =
        [
            (new DateTimeOffset(2026, 9, 12, 20, 0, 0, TimeSpan.Zero), 40),
            (new DateTimeOffset(2026, 9, 13, 20, 0, 0, TimeSpan.Zero), 50),
            (new DateTimeOffset(2026, 9, 14, 20, 0, 0, TimeSpan.Zero), 60)
        ];

        foreach ((DateTimeOffset windowEnd, int score) in mathWindows)
        {
            StudySession session = CreateFinishedSession(
                student, math, material, windowEnd.AddMinutes(-4));
            sessions.Add(session);
            windows.Add(CreateAnalyzedWindow(session.Id, windowEnd, score));
        }

        foreach ((DateTimeOffset windowEnd, int score) in biologyWindows)
        {
            StudySession session = CreateFinishedSession(
                student, biology, material, windowEnd.AddMinutes(-4));
            sessions.Add(session);
            windows.Add(CreateAnalyzedWindow(session.Id, windowEnd, score));
        }

        DateTimeOffset morningWindowEnd = new(2026, 9, 14, 6, 0, 0, TimeSpan.Zero);
        StudySession englishSession = CreateFinishedSession(
            student, english, material, morningWindowEnd.AddMinutes(-4));
        sessions.Add(englishSession);
        windows.Add(CreateAnalyzedWindow(englishSession.Id, morningWindowEnd, 75));

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudyMaterials.Add(material);
            db.StudySessions.AddRange(sessions);
            db.StudySessionBehaviorWindows.AddRange(windows);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(userId, "Student"));
        using HttpResponseMessage response = await client.GetAsync(
            "/api/progress/behavioral?range=Last30Days");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        JsonElement subjectInsights = root.GetProperty("subjects");
        JsonElement mathInsight = subjectInsights.EnumerateArray()
            .Single(item => item.GetProperty("subjectId").GetGuid() == math.Id);
        Assert.Equal(3, mathInsight.GetProperty("analyzedDayCount").GetInt32());
        Assert.Equal(80, mathInsight.GetProperty("latestFocusScore").GetInt32());
        Assert.Equal("Improving", mathInsight.GetProperty("focusTrend").GetString());
        Assert.Equal("EnoughData", mathInsight.GetProperty("focusDataStatus").GetString());

        JsonElement biologyInsight = subjectInsights.EnumerateArray()
            .Single(item => item.GetProperty("subjectId").GetGuid() == biology.Id);
        Assert.Equal("Improving", biologyInsight.GetProperty("focusTrend").GetString());
        Assert.Equal("EnoughData", biologyInsight.GetProperty("focusDataStatus").GetString());

        JsonElement englishInsight = subjectInsights.EnumerateArray()
            .Single(item => item.GetProperty("subjectId").GetGuid() == english.Id);
        Assert.Equal("InsufficientData", englishInsight.GetProperty("focusDataStatus").GetString());
        Assert.Equal(JsonValueKind.Null, englishInsight.GetProperty("focusTrend").ValueKind);

        JsonElement timeOfDay = root.GetProperty("timeOfDay");
        JsonElement afternoon = timeOfDay.EnumerateArray()
            .Single(item => item.GetProperty("period").GetString() == "Afternoon");
        Assert.Equal(3, afternoon.GetProperty("analyzedDayCount").GetInt32());
        Assert.Equal(70, afternoon.GetProperty("averageFocusScore").GetInt32());
        Assert.Equal("EnoughData", afternoon.GetProperty("focusDataStatus").GetString());

        JsonElement evening = timeOfDay.EnumerateArray()
            .Single(item => item.GetProperty("period").GetString() == "Evening");
        Assert.Equal(3, evening.GetProperty("analyzedDayCount").GetInt32());
        Assert.Equal("EnoughData", evening.GetProperty("focusDataStatus").GetString());
        Assert.Equal(50, evening.GetProperty("averageFocusScore").GetInt32());
        Assert.Equal("Afternoon", root.GetProperty("strongestTimeOfDay").GetString());
    }

    private static StudySessionBehaviorWindow CreateAnalyzedWindow(
        Guid sessionId,
        DateTimeOffset windowEnd,
        int focusScore)
    {
        StudySessionBehaviorWindow window = StudySessionBehaviorWindow.Create(
            sessionId, 1, windowEnd.AddMinutes(-3), windowEnd, false).Value;
        Assert.True(window.RecordAnalysis(
            focusScore,
            "NORMAL_FOCUSED",
            "STABLE",
            75,
            "STABLE",
            "CONTINUE",
            "CONTINUE",
            false,
            windowActiveTimeSeconds: 180).IsSuccess);
        return window;
    }

    [Fact]
    public async Task AnalyzeBehaviorWindow_CallsBehavioralIntelligence_AndPersistsResult()
    {
        DateTimeOffset now = new(2026, 9, 14, 22, 45, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory factory = new(now);

        Guid userId = Guid.NewGuid();
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        Student student = new(userId);
        student.ReplaceSubjects([subject]);

        StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
        StudyMaterial material = StudyMaterial.Create(
            student.Id,
            "material.pdf",
            1,
            1,
            $"materials/{session.Id}.pdf",
            StudyMaterialSource.Upload).Value;

        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(30).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        Assert.True(session.Start(now.AddMinutes(-5)).IsSuccess);

        Guid sectionId = Guid.NewGuid();
        StudySessionQuestion question = StudySessionQuestion.Create(
            session.Id,
            sectionId,
            "ai-section-1",
            "Section 1",
            "concept-1",
            "Concept 1",
            "What is 2 + 2?",
            ["2", "3", "4", "5"],
            "4",
            "easy",
            "Because 2 + 2 equals 4.",
            1,
            1).Value;

        DateTimeOffset shownAt = now.AddMinutes(-3);
        DateTimeOffset answeredAt = shownAt.AddSeconds(12);
        StudySessionQuestionAnswer answer = StudySessionQuestionAnswer.Create(
            question.Id,
            "4",
            true,
            null,
            1,
            answeredAt).Value;

        StudySessionBehaviorEvent scrollEvent = CreateEvent(
            session.Id,
            sectionId,
            null,
            StudySessionBehaviorEventType.Scroll,
            now.AddMinutes(-4),
            180,
            2,
            50);

        StudySessionBehaviorEvent shownEvent = CreateEvent(
            session.Id,
            sectionId,
            question.Id,
            StudySessionBehaviorEventType.QuestionShown,
            shownAt);

        StudySessionBehaviorEvent answeredEvent = CreateEvent(
            session.Id,
            sectionId,
            question.Id,
            StudySessionBehaviorEventType.QuestionAnswered,
            answeredAt);

        StudySessionBehaviorEvent interactionEvent = CreateEvent(
            session.Id,
            sectionId,
            null,
            StudySessionBehaviorEventType.Interaction,
            now.AddMinutes(-1),
            interactionCount: 2,
            progression: 80);

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudyMaterials.Add(material);
            db.StudySessions.Add(session);
            db.StudySessionQuestions.Add(question);
            db.StudySessionQuestionAnswers.Add(answer);
            db.StudySessionBehaviorEvents.AddRange(
                scrollEvent,
                shownEvent,
                answeredEvent,
                interactionEvent);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create(userId, "Student"));

        string body = """
                      { "isFinal": false }
                      """;

        HttpResponseMessage response = await client.PostAsync(
            $"/api/study-sessions/{session.Id}/behavior-windows/analyze",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The client supplies no timing or index. Repeating the request must return
        // the persisted window rather than analyzing it a second time.
        HttpResponseMessage repeatedResponse = await client.PostAsync(
            $"/api/study-sessions/{session.Id}/behavior-windows/analyze",
            new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);

        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("windowFocusScore", responseBody);
        Assert.Contains("windowUnderstandingScore", responseBody);
        Assert.Contains("windowActiveTimeSeconds", responseBody);

        HttpResponseMessage historyResponse = await client.GetAsync(
            $"/api/study-sessions/{session.Id}/behavior-windows");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        string historyBody = await historyResponse.Content.ReadAsStringAsync();
        Assert.Contains("windowIndex", historyBody);
        Assert.Contains("windowActiveTimeSeconds", historyBody);

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StudySessionBehaviorWindow persisted = Assert.Single(
            db.StudySessionBehaviorWindows.Where(item => item.StudySessionId == session.Id));

        Assert.Equal(1, persisted.WindowIndex);
        Assert.False(persisted.IsFinal);
        Assert.Equal(now.AddMinutes(-5), persisted.WindowStartUtc);
        Assert.Equal(now, persisted.WindowEndUtc);
        Assert.NotNull(persisted.FocusScore);
        Assert.NotNull(persisted.WindowActiveTimeSeconds);
        Assert.True(persisted.WindowActiveTimeSeconds is >= 0);
        Assert.Equal(100, persisted.UnderstandingScore);
        Assert.NotNull(persisted.RecommendedAction);
    }

    [Fact]
    public async Task EndingStudySession_EnqueuesFinalBehaviorAnalysis_AndWorkerPersistsFinalWindow()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory factory = new(now);

        Guid userId = Guid.NewGuid();
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        Student student = new(userId);
        student.ReplaceSubjects([subject]);

        StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
        StudyMaterial material = StudyMaterial.Create(
            student.Id,
            "final-analysis.pdf",
            1,
            1,
            $"materials/{session.Id}.pdf",
            StudyMaterialSource.Upload).Value;

        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(30).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        Assert.True(session.Start(now.AddMinutes(-6)).IsSuccess);

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudyMaterials.Add(material);
            db.StudySessions.Add(session);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create(userId, "Student"));

        HttpResponseMessage response = await client.PostAsync(
            $"/api/study-sessions/{session.Id}/end",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        StudySessionBehaviorAnalysisJob? completedJob = null;
        StudySessionBehaviorWindow? finalWindow = null;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(500);

            using IServiceScope scope = factory.Services.CreateScope();
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            completedJob = db.StudySessionBehaviorAnalysisJobs
                .SingleOrDefault(item => item.StudySessionId == session.Id);

            finalWindow = db.StudySessionBehaviorWindows
                .SingleOrDefault(item => item.StudySessionId == session.Id && item.IsFinal);

            if (completedJob?.Status == StudySessionBehaviorAnalysisJobStatus.Completed &&
                finalWindow is not null)
            {
                break;
            }
        }

        Assert.NotNull(completedJob);
        Assert.Equal(
            StudySessionBehaviorAnalysisJobStatus.Completed,
            completedJob!.Status);

        Assert.NotNull(finalWindow);
        Assert.Equal(session.Id, finalWindow!.StudySessionId);
        Assert.True(finalWindow.IsFinal);
        Assert.Equal(2, finalWindow.WindowIndex);
        Assert.NotNull(finalWindow.RecommendedAction);

        int windowCount;

        using (IServiceScope verificationScope = factory.Services.CreateScope())
        {
            ApplicationDbContext verificationDb =
                verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            windowCount = verificationDb.StudySessionBehaviorWindows
                .Count(item => item.StudySessionId == session.Id);
        }

        Assert.Equal(2, windowCount);
    }

    private static StudySessionBehaviorEvent CreateEvent(
        Guid sessionId,
        Guid? sectionId,
        Guid? questionId,
        StudySessionBehaviorEventType eventType,
        DateTimeOffset occurredAt,
        double? scrollSpeed = null,
        int? directionChanges = null,
        double? progression = null,
        int? interactionCount = null)
    {
        return StudySessionBehaviorEvent.Create(
            sessionId,
            sectionId,
            questionId,
            eventType,
            occurredAt,
            scrollSpeed,
            directionChanges,
            progression,
            interactionCount,
            null,
            null).Value;
    }

    private static StudySession CreateFinishedSession(
        Student student, StudentSubject subject, StudyMaterial material, DateTimeOffset startedAt)
    {
        StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(25).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        Assert.True(session.Start(startedAt).IsSuccess);
        Assert.True(session.End(startedAt.AddMinutes(5)).IsSuccess);
        return session;
    }
}