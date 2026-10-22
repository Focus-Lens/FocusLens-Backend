using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class GetChildSetupInvitationQueryHandlerTests
{
    [Fact]
    public async Task Get_WithValidToken_ReturnsInvitationPreview()
    {
        const string token = "valid-child-setup-token";
        ChildSetupInvitation invitation = CreateEmailInvitation(token);
        GetChildSetupInvitationQueryHandler handler = CreateHandler(invitation);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal("Email", result.Value.Type);
        Assert.Equal("CHILD@EXAMPLE.COM", result.Value.TargetEmail);
        Assert.Equal(invitation.ExpiresAtUtc, result.Value.ExpiresAtUtc);
    }

    [Fact]
    public async Task Get_WithLinkInvitation_ReturnsLinkTypeWithoutTargetEmail()
    {
        const string token = "link-child-setup-token";
        ChildSetupInvitation invitation = new(
            Guid.NewGuid(),
            ChildSetupInvitationType.Link,
            null,
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));
        GetChildSetupInvitationQueryHandler handler = CreateHandler(invitation);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Link", result.Value.Type);
        Assert.Null(result.Value.TargetEmail);
        Assert.Equal(invitation.ExpiresAtUtc, result.Value.ExpiresAtUtc);
    }

    [Fact]
    public async Task Get_WithUnknownToken_ReturnsNotFound()
    {
        ChildSetupInvitation invitation = CreateEmailInvitation("different-token");
        GetChildSetupInvitationQueryHandler handler = CreateHandler(invitation);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery("unknown-token"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
    }

    [Fact]
    public async Task Get_WithExpiredInvitation_ReturnsExpired()
    {
        const string token = "expired-token";
        ChildSetupInvitation invitation = new(
            Guid.NewGuid(),
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        GetChildSetupInvitationQueryHandler handler = CreateHandler(invitation);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.Expired", result.TopError.Code);
    }

    [Fact]
    public async Task Get_WithClaimedInvitationBySameStudent_ReturnsInvitationForResume()
    {
        const string token = "claimed-token";
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        ChildSetupDraft draft = new(Guid.NewGuid());
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));
        invitation.Claim(DateTimeOffset.UtcNow);
        GetChildSetupInvitationQueryHandler handler = CreateHandler(
            invitation,
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(userId));

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Claimed", result.Value.Status);
    }

    [Fact]
    public async Task Get_WithClaimedInvitationByDifferentStudent_ReturnsUnavailable()
    {
        const string token = "claimed-by-someone-else-token";
        Guid currentUserId = Guid.NewGuid();
        Student currentStudent = new(currentUserId);
        Student originalStudent = new(Guid.NewGuid());
        ChildSetupDraft draft = new(Guid.NewGuid());
        draft.MarkInvited();
        draft.MarkClaimed(originalStudent.Id);
        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Link,
            null,
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));
        invitation.Claim(DateTimeOffset.UtcNow);
        GetChildSetupInvitationQueryHandler handler = CreateHandler(
            invitation,
            new InMemoryRepository<Student>(currentStudent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(currentUserId));

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.Unavailable", result.TopError.Code);
    }

    [Fact]
    public async Task Get_WithActivatedInvitation_ReturnsUnavailable()
    {
        const string token = "activated-token";
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        ChildSetupDraft draft = new(Guid.NewGuid());
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);
        draft.MarkActivated();
        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Link,
            null,
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));
        invitation.Claim(DateTimeOffset.UtcNow);
        GetChildSetupInvitationQueryHandler handler = CreateHandler(
            invitation,
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(userId));

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.Unavailable", result.TopError.Code);
    }

    private static GetChildSetupInvitationQueryHandler CreateHandler(
        ChildSetupInvitation invitation,
        InMemoryRepository<Student>? students = null,
        InMemoryRepository<ChildSetupDraft>? drafts = null,
        FakeCurrentUser? currentUser = null) =>
        new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            students ?? new InMemoryRepository<Student>(),
            drafts ?? new InMemoryRepository<ChildSetupDraft>(),
            currentUser ?? new FakeCurrentUser(Guid.NewGuid()),
            TimeProvider.System);

    private static ChildSetupInvitation CreateEmailInvitation(string token) => new(
        Guid.NewGuid(),
        "CHILD@EXAMPLE.COM",
        Hash(token),
        DateTimeOffset.UtcNow.AddDays(1));

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
