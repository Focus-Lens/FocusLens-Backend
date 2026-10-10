using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FocusLens.Domain;
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