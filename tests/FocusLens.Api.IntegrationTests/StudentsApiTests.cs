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
    public async Task CompleteOnboarding_WhenStudyTimeGoalIsOmitted_LeavesOnboardingIncompleteWithMissingField()
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
            """{"preferredName":"Dina","goal":"FocusBetter","grade":"Grade10","subjects":[{"type":"Math","customName":null}],"dateOfBirth":"2010-05-12"}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/students/onboarding", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement profile = await GetProfileAsync(client);
        Assert.Equal("incomplete", profile.GetProperty("onboardingStatus").GetString());
        Assert.Contains("studyTimeGoal", profile.GetProperty("missingFields").EnumerateArray().Select(value => value.GetString()));
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
            """{"preferredName":"  Dina  ","goal":"FocusBetter","grade":"Grade10","subjects":[{"type":"Math","customName":null}],"studyTimeGoal":{"targetHours":1},"dateOfBirth":"2010-05-12"}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/students/onboarding", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement profile = await GetProfileAsync(client);
        Assert.Equal("completed", profile.GetProperty("onboardingStatus").GetString());
        Assert.Empty(profile.GetProperty("missingFields").EnumerateArray());

        HttpResponseMessage getResponse = await client.GetAsync("/api/students/me");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using JsonDocument getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal("Dina", getDocument.RootElement.GetProperty("preferredName").GetString());
        Assert.Equal("Weekly", getDocument.RootElement.GetProperty("studyTimeGoal").GetProperty("period").GetString());
        Assert.Equal(60, getDocument.RootElement.GetProperty("studyTimeGoal").GetProperty("targetMinutes").GetInt32());
    }

    [Fact]
    public async Task CompleteOnboarding_WhenDateOfBirthIsMissing_LeavesOnboardingIncomplete()
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
            """{"preferredName":"Dina","goal":"FocusBetter","grade":"Grade10","subjects":[{"type":"Math","customName":null}]}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/students/onboarding", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement profile = await GetProfileAsync(client);
        Assert.Equal("incomplete", profile.GetProperty("onboardingStatus").GetString());
        Assert.Contains("dateOfBirth", profile.GetProperty("missingFields").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("studyTimeGoal", profile.GetProperty("missingFields").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task CompleteOnboarding_WhenPreferredNameIsWhitespace_ReturnsBadRequest()
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
            """{"preferredName":"   "}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/students/onboarding", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

        await using CustomWebApplicationFactory factory = new();
        await factory.SeedAsync(db =>
        {
            SeedStudentIdentity(db, userId);
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
        Assert.Equal("incomplete", root.GetProperty("onboardingStatus").GetString());
        Assert.Contains("goal", root.GetProperty("missingFields").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("subjects", root.GetProperty("missingFields").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task UpdatePreferences_WhenDateOfBirthIsProvided_UpdatesAndReturnsDateOfBirth()
    {
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        student.SetDateOfBirth(new DateOnly(2010, 5, 12));

        await using CustomWebApplicationFactory factory = new();
        await factory.SeedAsync(db =>
        {
            SeedStudentIdentity(db, userId);
            db.Students.Add(student);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);
        using StringContent content = new(
            """{"dateOfBirth":"2011-06-15"}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PatchAsync("/api/students/me/preferences", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("2011-06-15", document.RootElement.GetProperty("dateOfBirth").GetString());

        HttpResponseMessage getResponse = await client.GetAsync("/api/students/me");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using JsonDocument getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal("2011-06-15", getDocument.RootElement.GetProperty("dateOfBirth").GetString());
    }

    [Fact]
    public async Task UpdatePreferences_WhenDateOfBirthIsNull_ClearsExistingDateOfBirth()
    {
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        student.SetDateOfBirth(new DateOnly(2010, 5, 12));

        await using CustomWebApplicationFactory factory = new();
        await factory.SeedAsync(db =>
        {
            SeedStudentIdentity(db, userId);
            db.Students.Add(student);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, userId);
        using StringContent content = new(
            """{"dateOfBirth":null}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PatchAsync("/api/students/me/preferences", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("dateOfBirth").ValueKind);
    }

    [Fact]
    public async Task UpdatePreferences_WhenCustomGradeIsOther_PersistsCustomGrade()
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
            """{"grade":"Other","customGrade":"  Year 13  "}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PatchAsync("/api/students/me/preferences", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Assert.Equal("Other", root.GetProperty("grade").GetString());
        Assert.Equal("Year 13", root.GetProperty("customGrade").GetString());

        HttpResponseMessage getResponse = await client.GetAsync("/api/students/me");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using JsonDocument getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        JsonElement getRoot = getDocument.RootElement;
        Assert.Equal("Other", getRoot.GetProperty("grade").GetString());
        Assert.Equal("Year 13", getRoot.GetProperty("customGrade").GetString());
    }

    [Fact]
    public async Task UpdatePreferences_WhenPreferredNameIsWhitespace_ReturnsBadRequest()
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
            SeedStudentIdentity(db, userId);
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

    private static async Task<JsonElement> GetProfileAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/api/students/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
