using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;
using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Api.IntegrationTests;

public class StudentInvitationApiTests
{
    [Fact]
    public async Task GetMyInvitations_AsStudent_IncludesRequestingParent()
    {
        InvitationFixture fixture = await CreateFixtureAsync();
        using HttpClient client = CreateStudentClient(fixture.Factory, fixture.StudentUserId);

        HttpResponseMessage response = await client.GetAsync("/api/access/invitations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement invitation = document.RootElement[0];
        Assert.Equal(fixture.RelationshipId, invitation.GetProperty("id").GetGuid());
        Assert.Equal("parent@example.com", invitation.GetProperty("otherPartyEmail").GetString());
        Assert.Equal("Incoming", invitation.GetProperty("direction").GetString());
        Assert.Equal("Relationship", invitation.GetProperty("kind").GetString());
        await fixture.Factory.DisposeAsync();
    }

    [Fact]
    public async Task AcceptInvitation_OnlyInvitedStudentCanAccept()
    {
        InvitationFixture fixture = await CreateFixtureAsync();
        Guid otherStudentUserId = Guid.NewGuid();

        using HttpClient otherStudentClient = CreateStudentClient(fixture.Factory, otherStudentUserId);

        HttpResponseMessage forbidden = await otherStudentClient.PostAsync(
            $"/api/access/invitations/{fixture.RelationshipId}/accept",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using HttpClient invitedStudentClient = CreateStudentClient(fixture.Factory, fixture.StudentUserId);
        HttpResponseMessage accepted = await invitedStudentClient.PostAsync(
            $"/api/access/invitations/{fixture.RelationshipId}/accept",
            null);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await fixture.Factory.DisposeAsync();
    }

    [Fact]
    public async Task DeclineInvitation_AsInvitedStudent_RevokesInvitation()
    {
        InvitationFixture fixture = await CreateFixtureAsync();
        using HttpClient client = CreateStudentClient(fixture.Factory, fixture.StudentUserId);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/access/invitations/{fixture.RelationshipId}/decline",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Revoked", document.RootElement.GetProperty("status").GetString());
        await fixture.Factory.DisposeAsync();
    }

    [Fact]
    public async Task ResolveInvitation_WithEmailInvitation_ReturnsTypeAndTargetEmail()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        const string rawToken = "student-parent-email-token";
        string tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        StudentParentInvitation invitation = new(
            student.Id,
            StudentParentInvitationType.Email,
            "PARENT@EXAMPLE.COM",
            tokenHash,
            DateTimeOffset.UtcNow.AddDays(7));
        CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(new ApplicationUser
            {
                Id = studentUserId,
                Email = "student@example.com",
                UserName = "student@example.com",
                FirstName = "Youssef",
                LastName = "Ali"
            });
            db.Students.Add(student);
            db.StudentParentInvitations.Add(invitation);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            $"/api/access/invitations/resolve?token={rawToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Assert.Equal("Email", root.GetProperty("type").GetString());
        Assert.Equal("PARENT@EXAMPLE.COM", root.GetProperty("targetEmail").GetString());

        await factory.DisposeAsync();
    }

    [Fact]
    public async Task ResolveInvitation_WithLinkInvitation_ReturnsLinkTypeWithoutTargetEmail()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        const string rawToken = "student-parent-link-token";
        string tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        StudentParentInvitation invitation = new(
            student.Id,
            StudentParentInvitationType.Link,
            null,
            tokenHash,
            DateTimeOffset.UtcNow.AddDays(7));
        CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(new ApplicationUser
            {
                Id = studentUserId,
                Email = "student@example.com",
                UserName = "student@example.com",
                FirstName = "Youssef",
                LastName = "Ali"
            });
            db.Students.Add(student);
            db.StudentParentInvitations.Add(invitation);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            $"/api/access/invitations/resolve?token={rawToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Assert.Equal("Link", root.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("targetEmail").ValueKind);

        await factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateInvitationLink_AsStudent_ReturnsLinkWithoutTargetEmail()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(new ApplicationUser
            {
                Id = studentUserId,
                Email = "student@example.com",
                UserName = "student@example.com"
            });
            db.Students.Add(student);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateStudentClient(factory, studentUserId);

        HttpResponseMessage response = await client.PostAsync(
            "/api/access/invitations/link/create",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        JsonElement root = document.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("id").GetGuid());
        Assert.Equal("Pending", root.GetProperty("status").GetString());
        Assert.Contains("/invitations/parent/", root.GetProperty("invitationUrl").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("otherPartyEmail").ValueKind);

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StudentParentInvitation invitation = await db.StudentParentInvitations.SingleAsync();

        Assert.Equal(StudentParentInvitationType.Link, invitation.Type);
        Assert.Null(invitation.TargetEmailNormalized);

        await factory.DisposeAsync();
    }

    private static async Task<InvitationFixture> CreateFixtureAsync()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        CustomWebApplicationFactory factory = new();

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new ApplicationUser
                {
                    Id = parentUserId,
                    Email = "parent@example.com",
                    UserName = "parent@example.com"
                },
                new ApplicationUser
                {
                    Id = studentUserId,
                    Email = "student@example.com",
                    UserName = "student@example.com"
                });

            db.Parents.Add(parent);
            db.Students.Add(student);
            db.ParentStudentRelationships.Add(relationship);
            return Task.CompletedTask;
        });

        return new InvitationFixture(factory, studentUserId, relationship.Id);
    }

    private static HttpClient CreateStudentClient(CustomWebApplicationFactory factory, Guid userId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create(userId, "Student"));
        return client;
    }

    private sealed record InvitationFixture(
        CustomWebApplicationFactory Factory,
        Guid StudentUserId,
        Guid RelationshipId);
}