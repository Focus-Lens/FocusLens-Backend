using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;
using FocusLens.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace FocusLens.Api.IntegrationTests;

public class StudentsApiTests
{
    [Fact]
    public async Task CompleteOnboarding_WhenAllFieldsAreSkipped_LeavesOnboardingIncomplete()
    {
        Guid userId = Guid.NewGuid();
        await using CustomWebApplicationFactory factory = new();
        await factory.SeedAsync(db =>
        {
            SeedStudentIdentity(db, userId);
            db.Students.Add(new Student(userId));
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);
        using StringContent content = new("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/students/onboarding", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await GetOnboardingCompletedAsync(client));
    }

    [Fact]
    public async Task CompleteOnboarding_WhenAllFieldsAreProvided_MarksOnboardingComplete()
    {
        Guid userId = Guid.NewGuid();
        await using CustomWebApplicationFactory factory = new();
        await factory.SeedAsync(db =>
        {
            SeedStudentIdentity(db, userId);
            db.Students.Add(new Student(userId));
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);
        using StringContent content = new(
            """{"goals":["FocusBetter"],"grade":"Grade10","subjects":[{"type":"Math","customName":null}],"studyPriorities":["StayFocused","ExamPreparation"],"studyTimeGoal":{"period":"Daily","targetMinutes":60,"days":["Monday","Tuesday","Wednesday","Thursday","Friday"],"startDate":null},"dateOfBirth":"2010-05-12"}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/students/onboarding", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await GetOnboardingCompletedAsync(client));
    }

    [Fact]
    public async Task UpdatePreferences_SupportsReplaceClearAndEmptySubjects()
    {
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        student.CompleteOnboarding(
            new[] { StudentGoal.FocusBetter },
            StudentGrade.Grade10,
            [StudentSubject.Predefined(StudentSubjectType.Math)]);

        await using CustomWebApplicationFactory factory = new();
        await factory.SeedAsync(db =>
        {
            db.Students.Add(student);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);
        using StringContent content = new(
            """{"preferredName":"  كريم  ","goals":null,"subjects":[]}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PatchAsync("/api/students/me/preferences", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Assert.Equal("كريم", root.GetProperty("preferredName").GetString());
        Assert.Equal(0, root.GetProperty("goals").GetArrayLength());
        Assert.Equal("Grade10", root.GetProperty("grade").GetString());
        Assert.Equal(0, root.GetProperty("subjects").GetArrayLength());
        Assert.False(root.GetProperty("onboardingCompleted").GetBoolean());
    }

    [Fact]
    public async Task UpdatePreferences_WhenPreferredNameIsWhitespace_ReturnsBadRequest()
    {
        Guid userId = Guid.NewGuid();
        await using CustomWebApplicationFactory factory = new();
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
        await using CustomWebApplicationFactory factory = new();
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

    private static void SeedStudentIdentity(ApplicationDbContext db, Guid userId)
    {
        ApplicationRole role = new()
        {
            Name = ApplicationRoles.Student, NormalizedName = ApplicationRoles.Student.ToUpperInvariant()
        };

        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            Email = "student@example.com",
            UserName = "student@example.com",
            FirstName = "Focus",
            LastName = "Student"
        });
        db.Roles.Add(role);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = role.Id });
    }

    private static async Task<bool> GetOnboardingCompletedAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/api/students/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("onboardingCompleted").GetBoolean();
    }
}