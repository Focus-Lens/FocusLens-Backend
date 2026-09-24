using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.Access;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public sealed class StudentParentInvitationLinkCommandHandlerTests
{
    [Fact]
    public async Task CreateLink_WithStudent_CreatesLinkWithoutEmail()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);

        InMemoryRepository<StudentParentInvitation> invitations = new();
        CreateStudentParentInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            invitations,
            new FakeCurrentUser(studentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new CreateStudentParentInvitationLinkCommand(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.NotNull(result.Value.InvitationUrl);
        Assert.Contains("/invitations/parent/", result.Value.InvitationUrl);

        StudentParentInvitation invitation = (await invitations.GetAllAsync()).Single();
        Assert.Equal(StudentParentInvitationType.Link, invitation.Type);
        Assert.Null(invitation.TargetEmailNormalized);
    }

    [Fact]
    public async Task CreateLink_WhenPendingEmailExists_ReturnsConflict()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        StudentParentInvitation invitation = new(
            student.Id,
            StudentParentInvitationType.Email,
            "PARENT@EXAMPLE.COM",
            "HASH",
            DateTimeOffset.UtcNow.AddDays(1));

        CreateStudentParentInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudentParentInvitation>(invitation),
            new FakeCurrentUser(studentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new CreateStudentParentInvitationLinkCommand(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Access.LinkInvitationAlreadyExists", result.TopError.Code);
    }

    [Fact]
    public async Task CreateLink_WhenPendingLinkExists_ReturnsConflict()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        StudentParentInvitation invitation = new(
            student.Id,
            StudentParentInvitationType.Link,
            null,
            "HASH",
            DateTimeOffset.UtcNow.AddDays(1));

        CreateStudentParentInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudentParentInvitation>(invitation),
            new FakeCurrentUser(studentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new CreateStudentParentInvitationLinkCommand(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Access.LinkInvitationAlreadyExists", result.TopError.Code);
    }

    [Fact]
    public async Task RegenerateLink_WithPendingLink_ReplacesTokenAndReturnsUrl()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        StudentParentInvitation invitation = new(
            student.Id,
            StudentParentInvitationType.Link,
            null,
            Hash("OLD_TOKEN"),
            DateTimeOffset.UtcNow.AddDays(1));
        InMemoryRepository<StudentParentInvitation> invitations = new(invitation);
        FakeUnitOfWork unitOfWork = new();

        RegenerateStudentParentInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            invitations,
            new FakeCurrentUser(studentUserId),
            unitOfWork,
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        DateTimeOffset oldExpiry = invitation.ExpiresAtUtc;

        Result<InvitationResponse> result = await handler.Handle(
            new RegenerateStudentParentInvitationLinkCommand(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(invitation.Id, result.Value.Id);
        Assert.Equal(StudentParentInvitationType.Link, invitation.Type);
        Assert.NotEqual(Hash("OLD_TOKEN"), invitation.TokenHash);
        Assert.NotEqual(oldExpiry, invitation.ExpiresAtUtc);
        Assert.Contains("/invitations/parent/", result.Value.InvitationUrl);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task RegenerateLink_WithEmailInvitation_ReturnsConflict()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        StudentParentInvitation invitation = new(
            student.Id,
            StudentParentInvitationType.Email,
            "PARENT@EXAMPLE.COM",
            "HASH",
            DateTimeOffset.UtcNow.AddDays(1));

        RegenerateStudentParentInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudentParentInvitation>(invitation),
            new FakeCurrentUser(studentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new RegenerateStudentParentInvitationLinkCommand(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Access.EmailLinkCannotBeRegenerated", result.TopError.Code);
    }

    [Fact]
    public async Task RegenerateLink_WithNoPendingInvitation_ReturnsNotFound()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);

        RegenerateStudentParentInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudentParentInvitation>(),
            new FakeCurrentUser(studentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new RegenerateStudentParentInvitationLinkCommand(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Access.InvitationNotFound", result.TopError.Code);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
