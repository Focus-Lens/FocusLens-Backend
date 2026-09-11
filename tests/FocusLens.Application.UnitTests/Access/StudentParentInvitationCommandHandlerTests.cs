using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public sealed class StudentParentInvitationCommandHandlerTests
{
    [Fact]
    public async Task Create_WithStudentAndValidEmail_CreatesAndEmailsInvitation()
    {
        Guid studentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        student.SetPreferredName("Youssef");
        student.SetPrivateProperty("User", new ApplicationUser
        {
            FirstName = "Youssef",
            LastName = "Ali"
        });

        var invitations = new InMemoryRepository<StudentParentInvitation>();
        var emailSender = new FakeEmailSender();
        var handler = new CreateStudentParentInvitationCommandHandler(
            new InMemoryRepository<Student>(student),
            invitations,
            new FakeCurrentUser(studentUserId),
            new FakeUnitOfWork(),
            emailSender,
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<StudentParentInvitationResponse> result = await handler.Handle(
            new CreateStudentParentInvitationCommand(
                new CreateStudentParentInvitationRequest("parent@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Contains("/invitations/parent/", result.Value.InvitationUrl);
        Assert.Single(emailSender.StudentParentInvitations);
        Assert.Equal("parent@example.com", emailSender.StudentParentInvitations[0].ParentEmail);
        Assert.Equal("Youssef", emailSender.StudentParentInvitations[0].StudentDisplayName);
    }

    [Fact]
    public async Task Respond_AcceptedByMatchingParent_ActivatesRelationship()
    {
        string token = "token-for-a-matching-parent";
        Guid studentUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        Parent parent = new(parentUserId);
        StudentParentInvitation invitation = new(
            student.Id,
            "PARENT@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));
        invitation.SetPrivateProperty("Student", student);

        var relationships = new InMemoryRepository<ParentStudentRelationship>();
        var handler = new RespondToStudentParentInvitationCommandHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<StudentParentInvitation>(invitation),
            relationships,
            new FakeCurrentUser(parentUserId, "parent@example.com"),
            new FakeUnitOfWork(),
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new RespondToStudentParentInvitationCommand(token, Accept: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value.Status);
        Assert.Equal(ParentInvitationStatus.Accepted, invitation.Status);
        Assert.Single(await relationships.GetAllAsync());
    }

    [Fact]
    public async Task Respond_WithDifferentParentEmail_ReturnsForbiddenAndDoesNotRespond()
    {
        string token = "token-for-a-different-parent";
        Guid studentUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        Parent parent = new(parentUserId);
        StudentParentInvitation invitation = new(
            student.Id,
            "PARENT@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));

        var handler = new RespondToStudentParentInvitationCommandHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<StudentParentInvitation>(invitation),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(parentUserId, "other@example.com"),
            new FakeUnitOfWork(),
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new RespondToStudentParentInvitationCommand(token, Accept: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Access.InvitationEmailMismatch", result.TopError.Code);
        Assert.Equal(ParentInvitationStatus.Pending, invitation.Status);
    }

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
