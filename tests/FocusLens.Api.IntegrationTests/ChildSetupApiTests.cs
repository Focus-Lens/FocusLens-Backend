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
        draft.SetGrade(StudentGrade.Grade10);
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

        Assert.Equal("Grade10", child.GetProperty("grade").GetString());

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

        Assert.Equal(RelationshipStatus.Active, relationship.Status);
    }

    [Fact]
    public async Task GetPendingInvitations_AsParent_ReturnsPendingChildInvitation()
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

        Assert.Equal("Pending", result.GetProperty("status").GetString());
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
            "YOUSSEF@EXAMPLE.COM",
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

        string expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))
        );

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
