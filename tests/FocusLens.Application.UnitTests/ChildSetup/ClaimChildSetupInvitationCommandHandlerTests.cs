using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class ClaimChildSetupInvitationCommandHandlerTests
{
    [Fact]
    public async Task Claim_LinkInvitation_DoesNotRequireEmailMatch()
    {
        const string token = "valid-link-token";
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        ChildSetupDraft draft = CreateInvitedDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Link,
            null,
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        ClaimChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(userId, "different@example.com"),
            new FakeUnitOfWork(),
            TimeProvider.System
        );

        Result<ClaimChildSetupInvitationResponse> result = await handler.Handle(
            new ClaimChildSetupInvitationCommand(token),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(ChildSetupStatus.Claimed, draft.Status);
        Assert.Equal(ChildSetupInvitationStatus.Claimed, invitation.Status);
    }

    [Fact]
    public async Task Claim_WithValidTokenAndMatchingEmail_ClaimsDraftAndInvitation()
    {
        const string token = "valid-child-token";
        const string email = "child@example.com";
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        ChildSetupDraft draft = CreateInvitedDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            email.ToUpperInvariant(),
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        ClaimChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(userId, email),
            new FakeUnitOfWork(),
            TimeProvider.System
        );

        Result<ClaimChildSetupInvitationResponse> result = await handler.Handle(
            new ClaimChildSetupInvitationCommand(token),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("Claimed", result.Value.DraftStatus);
        Assert.Equal("Claimed", result.Value.InvitationStatus);
        Assert.Equal(student.Id, draft.ClaimedByStudentId);
        Assert.Equal(ChildSetupStatus.Claimed, draft.Status);
        Assert.Equal(ChildSetupInvitationStatus.Claimed, invitation.Status);
        Assert.NotNull(invitation.ClaimedAtUtc);
    }

    [Fact]
    public async Task Claim_WithUnknownToken_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        ChildSetupDraft draft = CreateInvitedDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash("different-token"),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        ClaimChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(userId, "child@example.com"),
            new FakeUnitOfWork(),
            TimeProvider.System
        );

        Result<ClaimChildSetupInvitationResponse> result = await handler.Handle(
            new ClaimChildSetupInvitationCommand("unknown-token"),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
    }

    [Fact]
    public async Task Claim_WithDifferentEmail_ReturnsForbidden()
    {
        const string token = "email-mismatch-token";
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        ChildSetupDraft draft = CreateInvitedDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        ClaimChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(userId, "different@example.com"),
            new FakeUnitOfWork(),
            TimeProvider.System
        );

        Result<ClaimChildSetupInvitationResponse> result = await handler.Handle(
            new ClaimChildSetupInvitationCommand(token),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
        Assert.Equal(ChildSetupInvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public async Task Claim_WithExpiredInvitation_ReturnsExpired()
    {
        const string token = "expired-child-token";
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        ChildSetupDraft draft = CreateInvitedDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        ClaimChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(userId, "child@example.com"),
            new FakeUnitOfWork(),
            TimeProvider.System
        );

        Result<ClaimChildSetupInvitationResponse> result = await handler.Handle(
            new ClaimChildSetupInvitationCommand(token),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
    }

    [Fact]
    public async Task Claim_WithAlreadyClaimedInvitation_ReturnsConflict()
    {
        const string token = "claimed-child-token";
        Guid userId = Guid.NewGuid();
        Student student = new(userId);
        ChildSetupDraft draft = CreateInvitedDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1)
        );
        invitation.Claim(DateTimeOffset.UtcNow);

        ClaimChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(userId, "child@example.com"),
            new FakeUnitOfWork(),
            TimeProvider.System
        );

        Result<ClaimChildSetupInvitationResponse> result = await handler.Handle(
            new ClaimChildSetupInvitationCommand(token),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
    }

    [Fact]
    public async Task Claim_WhenStudentProfileDoesNotExist_ReturnsNotFound()
    {
        const string token = "missing-student-token";
        Guid userId = Guid.NewGuid();
        ChildSetupDraft draft = CreateInvitedDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        ClaimChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(userId, "child@example.com"),
            new FakeUnitOfWork(),
            TimeProvider.System
        );

        Result<ClaimChildSetupInvitationResponse> result = await handler.Handle(
            new ClaimChildSetupInvitationCommand(token),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("Students.NotFound", result.TopError.Code);
    }

    private static ChildSetupDraft CreateInvitedDraft(Guid parentId)
    {
        ChildSetupDraft draft = new(parentId);
        draft.SetName("Karim", "Mahmoud");
        draft.MarkInvited();
        return draft;
    }

    private static string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
