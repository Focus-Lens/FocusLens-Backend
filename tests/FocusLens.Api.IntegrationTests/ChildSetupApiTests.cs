using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;
using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Api.IntegrationTests;

public sealed class ChildSetupApiTests
{
    [Fact]
    public async Task ActivateChildSetup_MakesChildVisibleToParent()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(StudentGrade.Other);
        draft.SetCustomGrade("  Year 13  ");
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = studentUserId,
                    Email = "youssef@example.com",
                    UserName = "youssef@example.com",
                    FirstName = "Old",
                    LastName = "Name"
                }
            );

            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );

            db.Parents.Add(parent);
            db.Students.Add(student);
            db.ChildSetupDrafts.Add(draft);

            return Task.CompletedTask;
        });

        using HttpClient studentClient = CreateClient(factory, studentUserId, "Student");

        HttpResponseMessage activationResponse = await studentClient.PostAsync(
            "/api/students/me/child-setup/activate",
            null
        );

        Assert.Equal(HttpStatusCode.OK, activationResponse.StatusCode);

        using HttpClient parentClient = CreateClient(factory, parentUserId, "Parent");

        HttpResponseMessage studentsResponse = await parentClient.GetAsync("/api/access/students");

        Assert.Equal(HttpStatusCode.OK, studentsResponse.StatusCode);

        using JsonDocument document = JsonDocument.Parse(
            await studentsResponse.Content.ReadAsStringAsync()
        );

        JsonElement students = document.RootElement;

        Assert.Equal(1, students.GetArrayLength());

        JsonElement child = students[0];

        Assert.Equal(student.Id, child.GetProperty("id").GetGuid());

        Assert.Equal("Youssef", child.GetProperty("firstName").GetString());

        Assert.Equal("Mahmoud", child.GetProperty("lastName").GetString());

        Assert.Equal("Other", child.GetProperty("grade").GetString());

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChildSetupDraft persistedDraft = await db.ChildSetupDrafts.SingleAsync(item =>
            item.Id == draft.Id
        );

        ParentStudentRelationship relationship =
            await db.ParentStudentRelationships.SingleAsync(item =>
                item.ParentId == parent.Id && item.StudentId == student.Id
            );

        Assert.Equal(ChildSetupStatus.Activated, persistedDraft.Status);
        Student persistedStudent = await db.Students.SingleAsync(item => item.Id == student.Id);
        Assert.Equal(StudentGrade.Other, persistedStudent.Grade);
        Assert.Equal("Year 13", persistedStudent.CustomGrade);
        Assert.Equal("Year 13", persistedDraft.CustomGrade);

        Assert.Equal(RelationshipStatus.Active, relationship.Status);
    }

    [Fact]
    public async Task GetInvitation_WithEmailInvitation_ReturnsInvitationTypeAndTargetEmail()
    {
        const string token = "email-child-setup-token";
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(StudentGrade.Other);
        draft.SetCustomGrade("Year 13");
        draft.MarkInvited();

        ChildSetupInvitation invitation = new(
            draft.Id,
            "YOUSSEF@EXAMPLE.COM",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );

            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            db.ChildSetupInvitations.Add(invitation);

            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            $"/api/child-setup-invitations/{token}"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );

        JsonElement result = document.RootElement;

        Assert.Equal("Pending", result.GetProperty("status").GetString());
        Assert.Equal("Email", result.GetProperty("type").GetString());
        Assert.Equal("YOUSSEF@EXAMPLE.COM", result.GetProperty("targetEmail").GetString());
        Assert.Equal(invitation.ExpiresAtUtc, result.GetProperty("expiresAtUtc").GetDateTimeOffset());
        Assert.Equal(4, result.EnumerateObject().Count());
        Assert.False(result.TryGetProperty("firstName", out _));
        Assert.False(result.TryGetProperty("lastName", out _));
        Assert.False(result.TryGetProperty("grade", out _));
        Assert.False(result.TryGetProperty("customGrade", out _));
        Assert.False(result.TryGetProperty("subjects", out _));
        Assert.False(result.TryGetProperty("goal", out _));
        Assert.False(result.TryGetProperty("studyTimeGoal", out _));
    }

    [Fact]
    public async Task GetInvitation_WithLinkInvitation_ReturnsLinkTypeWithoutTargetEmail()
    {
        const string token = "link-child-setup-token";
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(StudentGrade.Grade10);
        draft.MarkInvited();

        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Link,
            null,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );

            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            db.ChildSetupInvitations.Add(invitation);

            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            $"/api/child-setup-invitations/{token}"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );

        JsonElement result = document.RootElement;

        Assert.Equal("Pending", result.GetProperty("status").GetString());
        Assert.Equal("Link", result.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("targetEmail").ValueKind);
        Assert.Equal(invitation.ExpiresAtUtc, result.GetProperty("expiresAtUtc").GetDateTimeOffset());
        Assert.Equal(4, result.EnumerateObject().Count());
        Assert.False(result.TryGetProperty("firstName", out _));
        Assert.False(result.TryGetProperty("lastName", out _));
        Assert.False(result.TryGetProperty("grade", out _));
        Assert.False(result.TryGetProperty("customGrade", out _));
        Assert.False(result.TryGetProperty("subjects", out _));
        Assert.False(result.TryGetProperty("goal", out _));
        Assert.False(result.TryGetProperty("studyTimeGoal", out _));
    }

    [Fact]
    public async Task GetPendingInvitations_AsParent_ReturnsPendingChildInvitation()
    {
        Guid parentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(StudentGrade.Other);
        draft.SetCustomGrade("Year 13");
        draft.MarkInvited();

        ChildSetupInvitation invitation = new(
            draft.Id,
            "YOUSSEF@EXAMPLE.COM",
            "test-hash",
            DateTimeOffset.UtcNow.AddDays(7)
        );

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );

            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            db.ChildSetupInvitations.Add(invitation);

            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");

        HttpResponseMessage response = await client.GetAsync(
            "/api/parents/child-setups/invitations"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );

        JsonElement invitations = document.RootElement;

        Assert.Equal(1, invitations.GetArrayLength());

        JsonElement result = invitations[0];

        Assert.Equal(draft.Id, result.GetProperty("draftId").GetGuid());

        Assert.Equal(invitation.Id, result.GetProperty("invitationId").GetGuid());

        Assert.Equal("Youssef", result.GetProperty("firstName").GetString());

        Assert.Equal("Mahmoud", result.GetProperty("lastName").GetString());

        Assert.Equal("YOUSSEF@EXAMPLE.COM", result.GetProperty("targetEmail").GetString());
        Assert.Equal("Other", result.GetProperty("grade").GetString());
        Assert.Equal("Year 13", result.GetProperty("customGrade").GetString());

        Assert.Equal("Pending", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateChildSetup_WithOtherGrade_PersistsAndReturnsTrimmedCustomGrade()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );

            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);

            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");

        using StringContent content = new(
            """{"firstName":"Youssef","lastName":"Mahmoud","dateOfBirth":null,"grade":"Other","customGrade":"  Year 13  ","subjects":[],"studyPriorities":[],"studyTimeGoal":null}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PutAsync(
            $"/api/parents/child-setups/{draft.Id}",
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        JsonElement result = document.RootElement;

        Assert.Equal("Other", result.GetProperty("grade").GetString());
        Assert.Equal("Year 13", result.GetProperty("customGrade").GetString());

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ChildSetupDraft persistedDraft = await db.ChildSetupDrafts.SingleAsync(item => item.Id == draft.Id);

        Assert.Equal(StudentGrade.Other, persistedDraft.Grade);
        Assert.Equal("Year 13", persistedDraft.CustomGrade);
    }

    [Fact]
    public async Task UpdateChildSetup_WithTargetHours_DerivesWeeklyGoalFromParentSettings()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        ChildSetupDraft draft = new(parent.Id);

        await using CustomWebApplicationFactory factory = new(
            new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));

        await factory.SeedAsync(db =>
        {
            db.Users.Add(new ApplicationUser
            {
                Id = parentUserId,
                Email = "parent@example.com",
                UserName = "parent@example.com",
                FirstName = "Mariam",
                LastName = "Parent"
            });
            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");
        using StringContent content = new(
            """{"firstName":"Youssef","lastName":"Mahmoud","dateOfBirth":"2010-05-12","grade":"Grade10","customGrade":null,"subjects":[{"type":"Math","customName":null}],"goal":"FocusBetter","studyTimeGoal":{"targetHours":5}}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PutAsync(
            $"/api/parents/child-setups/{draft.Id}",
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement studyTimeGoal = document.RootElement.GetProperty("studyTimeGoal");
        Assert.Equal("Weekly", studyTimeGoal.GetProperty("period").GetString());
        Assert.Equal(300, studyTimeGoal.GetProperty("targetMinutes").GetInt32());
        Assert.Equal("Saturday", studyTimeGoal.GetProperty("days")[0].GetString());
        Assert.Equal("2026-09-12", studyTimeGoal.GetProperty("startDate").GetString());
    }

    [Fact]
    public async Task UpdateChildSetup_WithOtherGradeWithoutCustomGrade_ReturnsBadRequest()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );
            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");
        using StringContent content = new(
            """{"firstName":"Youssef","lastName":"Mahmoud","dateOfBirth":null,"grade":"Other","customGrade":null,"subjects":[],"studyPriorities":[],"studyTimeGoal":null}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PutAsync(
            $"/api/parents/child-setups/{draft.Id}",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateChildSetup_WithPredefinedGradeAndCustomGrade_ReturnsBadRequest()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );
            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");
        using StringContent content = new(
            """{"firstName":"Youssef","lastName":"Mahmoud","dateOfBirth":null,"grade":"Grade12","customGrade":"Year 13","subjects":[],"studyPriorities":[],"studyTimeGoal":null}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PutAsync(
            $"/api/parents/child-setups/{draft.Id}",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateChildSetup_SwitchingToPredefinedGrade_ClearsCustomGrade()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(StudentGrade.Other);
        draft.SetCustomGrade("Year 13");

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );
            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");
        using StringContent content = new(
            """{"firstName":"Youssef","lastName":"Mahmoud","dateOfBirth":null,"grade":"Grade12","customGrade":null,"subjects":[],"studyPriorities":[],"studyTimeGoal":null}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PutAsync(
            $"/api/parents/child-setups/{draft.Id}",
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal("Grade12", document.RootElement.GetProperty("grade").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("customGrade").ValueKind);

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ChildSetupDraft persistedDraft = await db.ChildSetupDrafts.SingleAsync(item => item.Id == draft.Id);

        Assert.Equal(StudentGrade.Grade12, persistedDraft.Grade);
        Assert.Null(persistedDraft.CustomGrade);
    }

    [Fact]
    public async Task UpdateChildSetup_WithCustomGradeLongerThan100Characters_ReturnsBadRequest()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );
            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");
        string customGrade = new('X', 101);
        string json =
            $"{{\"firstName\":\"Youssef\",\"lastName\":\"Mahmoud\",\"dateOfBirth\":null,\"grade\":\"Other\",\"customGrade\":\"{customGrade}\",\"subjects\":[],\"studyPriorities\":[],\"studyTimeGoal\":null}}";
        using StringContent content = new(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PutAsync(
            $"/api/parents/child-setups/{draft.Id}",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelInvitation_AsParent_CancelsInvitationAndResetsDraft()
    {
        Guid parentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(StudentGrade.Grade10);
        draft.MarkInvited();

        ChildSetupInvitation invitation = new(
            draft.Id,
            "YOUSSEF@EXAMPLE.COM",
            "test-hash",
            DateTimeOffset.UtcNow.AddDays(7)
        );

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );

            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            db.ChildSetupInvitations.Add(invitation);

            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");

        HttpResponseMessage response = await client.PostAsync(
            $"/api/parents/child-setups/{draft.Id}/invite/cancel",
            null
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChildSetupDraft persistedDraft = await db.ChildSetupDrafts.SingleAsync(item =>
            item.Id == draft.Id
        );

        ChildSetupInvitation persistedInvitation =
            await db.ChildSetupInvitations.SingleAsync(item => item.Id == invitation.Id
            );

        Assert.Equal(ChildSetupStatus.Draft, persistedDraft.Status);

        Assert.Equal(ChildSetupInvitationStatus.Cancelled, persistedInvitation.Status);
    }

    [Fact]
    public async Task GetInvitationLink_AsParent_RenewsInvitationAndReturnsLink()
    {
        Guid parentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(StudentGrade.Grade10);
        draft.MarkInvited();

        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Link,
            null,
            "OLD-HASH",
            DateTimeOffset.UtcNow.AddDays(1)
        );

        DateTimeOffset oldExpiry = invitation.ExpiresAtUtc;
        string oldHash = invitation.TokenHash;

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );

            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            db.ChildSetupInvitations.Add(invitation);

            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");

        HttpResponseMessage response = await client.PostAsync(
            $"/api/parents/child-setups/{draft.Id}/invite/link",
            null
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );

        JsonElement result = document.RootElement;

        string invitationUrl = result.GetProperty("invitationUrl").GetString()!;

        Assert.False(string.IsNullOrWhiteSpace(invitationUrl));
        Assert.Equal(invitation.Id, result.GetProperty("id").GetGuid());
        Assert.Equal("Pending", result.GetProperty("status").GetString());

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChildSetupInvitation persistedInvitation =
            await db.ChildSetupInvitations.SingleAsync(item => item.Id == invitation.Id
            );

        Assert.Equal(ChildSetupInvitationStatus.Pending, persistedInvitation.Status);

        Assert.NotEqual(oldHash, persistedInvitation.TokenHash);
        Assert.NotEqual(oldExpiry, persistedInvitation.ExpiresAtUtc);

        string token = invitationUrl.TrimEnd('/').Split('/').Last();

        string expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        Assert.Equal(expectedHash, persistedInvitation.TokenHash);
    }

    [Fact]
    public async Task ResendInvitation_AsParent_RenewsInvitationAndReturnsNewLink()
    {
        Guid parentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(StudentGrade.Grade10);
        draft.MarkInvited();

        ChildSetupInvitation invitation = new(
            draft.Id,
            "YOUSSEF@EXAMPLE.COM",
            "OLD-HASH",
            DateTimeOffset.UtcNow.AddDays(1)
        );

        string oldHash = invitation.TokenHash;
        DateTimeOffset oldExpiry = invitation.ExpiresAtUtc;

        await using CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com",
                    FirstName = "Mariam",
                    LastName = "Parent"
                }
            );

            db.Parents.Add(parent);
            db.ChildSetupDrafts.Add(draft);
            db.ChildSetupInvitations.Add(invitation);

            return Task.CompletedTask;
        });

        using HttpClient client = CreateClient(factory, parentUserId, "Parent");

        HttpResponseMessage response = await client.PostAsync(
            $"/api/parents/child-setups/{draft.Id}/invite/resend",
            null
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );

        JsonElement result = document.RootElement;

        Assert.Equal(invitation.Id, result.GetProperty("id").GetGuid());

        Assert.Equal("Pending", result.GetProperty("status").GetString());

        string invitationUrl = result.GetProperty("invitationUrl").GetString()!;

        Assert.False(string.IsNullOrWhiteSpace(invitationUrl));

        using IServiceScope scope = factory.Services.CreateScope();

        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChildSetupInvitation persistedInvitation =
            await db.ChildSetupInvitations.SingleAsync(item => item.Id == invitation.Id
            );

        Assert.Equal(ChildSetupInvitationStatus.Pending, persistedInvitation.Status);

        Assert.NotEqual(oldHash, persistedInvitation.TokenHash);

        Assert.True(persistedInvitation.ExpiresAtUtc > oldExpiry);
    }

    private static HttpClient CreateClient(
        CustomWebApplicationFactory factory,
        Guid userId,
        string role
    )
    {
        HttpClient client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create(userId, role)
        );

        return client;
    }
}