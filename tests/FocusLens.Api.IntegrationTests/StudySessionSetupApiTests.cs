using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FocusLens.Domain;
using FocusLens.Domain.StudySessions;
using FocusLens.Domain.Students;

namespace FocusLens.Api.IntegrationTests;

public sealed class StudySessionSetupApiTests
{
    [Fact]
    public async Task SetMode_ReturnsNoContent()
    {
        await using CustomWebApplicationFactory factory = new();
        (Guid userId, StudySession session, _) = await SeedSessionAsync(factory);
        using HttpClient client = CreateStudentClient(factory, userId);

        HttpResponseMessage response = await client.PutAsync(
            $"/api/study-sessions/{session.Id}/mode",
            JsonContent("""{"mode":"Paper"}"""));

        await AssertNoContentAsync(response);
    }

    [Fact]
    public async Task SetSubject_ReturnsNoContent()
    {
        await using CustomWebApplicationFactory factory = new();
        (Guid userId, StudySession session, StudentSubject subject) = await SeedSessionAsync(factory);
        using HttpClient client = CreateStudentClient(factory, userId);

        HttpResponseMessage response = await client.PutAsync(
            $"/api/study-sessions/{session.Id}/subject",
            JsonContent($$"""{"subjectId":"{{subject.Id}}"}"""));

        await AssertNoContentAsync(response);
    }

    [Fact]
    public async Task SetDuration_ReturnsNoContent()
    {
        await using CustomWebApplicationFactory factory = new();
        (Guid userId, StudySession session, _) = await SeedSessionAsync(factory);
        using HttpClient client = CreateStudentClient(factory, userId);

        HttpResponseMessage response = await client.PutAsync(
            $"/api/study-sessions/{session.Id}/duration",
            JsonContent("""{"focusDurationMinutes":45}"""));

        await AssertNoContentAsync(response);
    }

    [Fact]
    public async Task Start_ReturnsNoContent()
    {
        await using CustomWebApplicationFactory factory = new();
        (Guid userId, StudySession session, _) = await SeedSessionAsync(factory, readyToStart: true);
        using HttpClient client = CreateStudentClient(factory, userId);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/study-sessions/{session.Id}/start",
            null);

        await AssertNoContentAsync(response);
    }

    private static async Task<(Guid UserId, StudySession Session, StudentSubject Subject)> SeedSessionAsync(
        CustomWebApplicationFactory factory,
        bool readyToStart = false)
    {
        Guid userId = Guid.NewGuid();
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        Student student = new(userId);
        student.ReplaceSubjects([subject]);
        StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;

        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            db.StudySessions.Add(session);

            if (readyToStart)
            {
                StudyMaterial material = StudyMaterial.Create(
                    student.Id,
                    "material.pdf",
                    1,
                    1,
                    "materials/original.pdf",
                    StudyMaterialSource.Upload).Value;
                Assert.True(session.SetSubject(subject).IsSuccess);
                Assert.True(session.SetDuration(25).IsSuccess);
                Assert.True(session.SetStudyMaterial(material).IsSuccess);
                db.StudyMaterials.Add(material);
            }

            return Task.CompletedTask;
        });

        return (userId, session, subject);
    }

    private static HttpClient CreateStudentClient(CustomWebApplicationFactory factory, Guid userId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create(userId, "Student"));
        return client;
    }

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private static async Task AssertNoContentAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }
}
