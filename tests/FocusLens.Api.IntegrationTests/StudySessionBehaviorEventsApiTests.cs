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

public sealed class StudySessionBehaviorEventsApiTests
{
    [Fact]
    public async Task RecordBehaviorEvents_ForOwnedStartedSession_PersistsTheBatch()
    {
        await using CustomWebApplicationFactory factory = new();
        (Guid userId, StudySession session) = await SeedStartedSessionAsync(factory);
        using HttpClient client = CreateStudentClient(factory, userId);
        string body = $$"""
        {
          "events": [
            {
              "eventType": "Scroll",
              "occurredAtUtc": "{{session.StartedAtUtc!.Value.AddSeconds(1):O}}",
              "scrollSpeedAvgPxPerSec": 240,
              "scrollDirectionChanges": 2,
              "contentProgressionPct": 45,
              "interactionCount": 1
            },
            {
              "eventType": "AppBackground",
              "occurredAtUtc": "{{session.StartedAtUtc!.Value.AddSeconds(2):O}}",
              "backgroundCount": 1,
              "backgroundDurationSeconds": 5
            }
          ]
        }
        """;

        HttpResponseMessage response = await client.PostAsync(
            $"/api/study-sessions/{session.Id}/behavior-events", JsonContent(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("acceptedCount").GetInt32());

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StudySessionBehaviorEvent[] events = db.StudySessionBehaviorEvents
            .Where(item => item.StudySessionId == session.Id)
            .OrderBy(item => item.OccurredAtUtc)
            .ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal(StudySessionBehaviorEventType.Scroll, events[0].EventType);
        Assert.Equal(240, events[0].ScrollSpeedAvgPxPerSec);
        Assert.Equal(StudySessionBehaviorEventType.AppBackground, events[1].EventType);
    }

    [Fact]
    public async Task RecordBehaviorEvents_WithMoreThanOneHundredEvents_ReturnsBadRequestAndPersistsNothing()
    {
        await using CustomWebApplicationFactory factory = new();
        (Guid userId, StudySession session) = await SeedStartedSessionAsync(factory);
        using HttpClient client = CreateStudentClient(factory, userId);
        object payload = new
        {
            events = Enumerable.Range(0, 101).Select(index => new
            {
                eventType = "Interaction",
                occurredAtUtc = session.StartedAtUtc!.Value.AddSeconds(index)
            })
        };

        HttpResponseMessage response = await client.PostAsync(
            $"/api/study-sessions/{session.Id}/behavior-events",
            JsonContent(JsonSerializer.Serialize(payload)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(db.StudySessionBehaviorEvents.Where(item => item.StudySessionId == session.Id));
    }

    private static async Task<(Guid UserId, StudySession Session)> SeedStartedSessionAsync(
        CustomWebApplicationFactory factory)
    {
        Guid userId = Guid.NewGuid();
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        Student student = new(userId);
        student.ReplaceSubjects([subject]);
        StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
        StudyMaterial material = StudyMaterial.Create(student.Id, "material.pdf", 1, 1,
            $"materials/{session.Id}.pdf", StudyMaterialSource.Upload).Value;
        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(30).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        Assert.True(session.Start(DateTimeOffset.UtcNow.AddMinutes(-1)).IsSuccess);

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudyMaterials.Add(material);
            db.StudySessions.Add(session);
            return Task.CompletedTask;
        });
        return (userId, session);
    }

    private static HttpClient CreateStudentClient(CustomWebApplicationFactory factory, Guid userId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(userId, "Student"));
        return client;
    }

    private static StringContent JsonContent(string json) => new(json, Encoding.UTF8, "application/json");
}
