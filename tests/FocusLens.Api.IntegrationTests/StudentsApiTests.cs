using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FocusLens.Domain;
using FocusLens.Domain.Students;

namespace FocusLens.Api.IntegrationTests;

public class StudentsApiTests
{
    [Fact]
    public async Task CompleteOnboarding_WhenAllFieldsAreSkipped_LeavesOnboardingIncomplete()
    {
        Guid userId = Guid.NewGuid();
        await using var factory = new CustomWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Students.Add(new Student(userId));
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);
        using StringContent content = new("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/students/onboarding", content);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await GetOnboardingCompletedAsync(client));
    }

    [Fact]
    public async Task CompleteOnboarding_WhenAllFieldsAreProvided_MarksOnboardingComplete()
    {
        Guid userId = Guid.NewGuid();
        await using var factory = new CustomWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Students.Add(new Student(userId));
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);
        using StringContent content = new(
            """{"goal":"FocusBetter","grade":"Grade10","subjects":[{"type":"Math","customName":null}]}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/students/onboarding", content);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await GetOnboardingCompletedAsync(client));
    }

    [Fact]
    public async Task UpdatePreferences_SupportsReplaceClearAndEmptySubjects()
    {
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        student.CompleteOnboarding(
            StudentGoal.FocusBetter,
            StudentGrade.Grade10,
            [StudentSubject.Predefined(StudentSubjectType.Math)]);

        await using var factory = new CustomWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);
        using StringContent content = new(
            """{"preferredName":"  كريم  ","goal":null,"subjects":[]}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PatchAsync("/api/students/me/preferences", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Assert.Equal("كريم", root.GetProperty("preferredName").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("goal").ValueKind);
        Assert.Equal("Grade10", root.GetProperty("grade").GetString());
        Assert.Equal(0, root.GetProperty("subjects").GetArrayLength());
        Assert.False(root.GetProperty("onboardingCompleted").GetBoolean());
    }

    [Fact]
    public async Task UpdatePreferences_WhenPreferredNameIsWhitespace_ReturnsBadRequest()
    {
        Guid userId = Guid.NewGuid();
        await using var factory = new CustomWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Students.Add(new Student(userId));
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);
        using StringContent content = new(
            """{"preferredName":"   "}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PatchAsync("/api/students/me/preferences", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithNoParentRelationship_ReturnsStudentProfile()
    {
        Guid userId = Guid.NewGuid();
        await using var factory = new CustomWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Students.Add(new Student(userId));
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);

        HttpResponseMessage response = await client.GetAsync("/api/students/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpClient CreateStudentClient(CustomWebApplicationFactory factory, Guid userId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create(userId, "Student"));
        return client;
    }

    private static async Task<bool> GetOnboardingCompletedAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/api/students/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("onboardingCompleted").GetBoolean();
    }
}
