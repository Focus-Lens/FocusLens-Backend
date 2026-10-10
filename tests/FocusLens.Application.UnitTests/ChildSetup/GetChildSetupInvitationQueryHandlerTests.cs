using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class GetChildSetupInvitationQueryHandlerTests
{
    [Fact]
    public async Task Get_WithValidToken_ReturnsInvitationPreview()
    {
        const string token = "valid-child-setup-token";
        ChildSetupInvitation invitation = new(
            Guid.NewGuid(),
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            TimeProvider.System);

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

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            TimeProvider.System);

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
        ChildSetupInvitation invitation = new(
            Guid.NewGuid(),
            "CHILD@EXAMPLE.COM",
            Hash("different-token"),
            DateTimeOffset.UtcNow.AddDays(1));

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            TimeProvider.System);

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

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            TimeProvider.System);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.Expired", result.TopError.Code);
    }

    [Fact]
    public async Task Get_WithClaimedInvitation_ReturnsUnavailable()
    {
        const string token = "claimed-token";
        ChildSetupInvitation invitation = new(
            Guid.NewGuid(),
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));
        invitation.Claim(DateTimeOffset.UtcNow);

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            TimeProvider.System);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.Unavailable", result.TopError.Code);
    }

    private static string Hash(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}