using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public class DeclineInvitationCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithCorrectStudent_RevokesInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { Email = "parent@example.com" });
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Student", student);
        relationship.SetPrivateProperty("Parent", parent);

        InMemoryRepository<ParentStudentRelationship> relationshipRepository = new(relationship);
        FakeUnitOfWork unitOfWork = new();

        DeclineInvitationCommandHandler handler = new(
            relationshipRepository,
            new InMemoryRepository<StudentParentInvitation>(),
            new InMemoryRepository<Parent>(),
            new FakeCurrentUser(studentUserId),
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new DeclineInvitationCommand(relationship.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Revoked", result.Value.Status);
        Assert.Equal(RelationshipStatus.Revoked, relationship.Status);
        Assert.NotNull(relationship.RevokedAtUtc);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithDifferentStudent_ReturnsForbidden()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();
        Guid otherStudentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { Email = "parent@example.com" });
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Student", student);
        relationship.SetPrivateProperty("Parent", parent);

        FakeUnitOfWork unitOfWork = new();

        DeclineInvitationCommandHandler handler = new(
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<StudentParentInvitation>(),
            new InMemoryRepository<Parent>(),
            new FakeCurrentUser(otherStudentUserId),
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new DeclineInvitationCommand(relationship.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.TopError.Type);
        Assert.Equal("Access.InvitationNotOwned", result.TopError.Code);
        Assert.Equal(RelationshipStatus.Pending, relationship.Status);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithMissingInvitation_ReturnsNotFound()
    {
        Guid studentUserId = Guid.NewGuid();
        FakeUnitOfWork unitOfWork = new();

        DeclineInvitationCommandHandler handler = new(
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<StudentParentInvitation>(),
            new InMemoryRepository<Parent>(),
            new FakeCurrentUser(studentUserId),
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new DeclineInvitationCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("Access.InvitationNotFound", result.TopError.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenInvitationIsNotPending_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { Email = "parent@example.com" });
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Student", student);
        relationship.SetPrivateProperty("Parent", parent);
        relationship.Accept();

        FakeUnitOfWork unitOfWork = new();

        DeclineInvitationCommandHandler handler = new(
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<StudentParentInvitation>(),
            new InMemoryRepository<Parent>(),
            new FakeCurrentUser(studentUserId),
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new DeclineInvitationCommand(relationship.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.TopError.Type);
        Assert.Equal("Access.InvalidInvitationState", result.TopError.Code);
        Assert.Equal(RelationshipStatus.Active, relationship.Status);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithMatchingParentInvitation_DeclinesInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { Email = "parent@example.com" });
        Student student = new(studentUserId);

        StudentParentInvitation invitation = new(
            student.Id,
            "PARENT@EXAMPLE.COM",
            "TEST-HASH",
            DateTimeOffset.UtcNow.AddDays(1));

        invitation.SetPrivateProperty("Student", student);

        InMemoryRepository<StudentParentInvitation> invitations = new(invitation);
        FakeUnitOfWork unitOfWork = new();

        DeclineInvitationCommandHandler handler = new(
            new InMemoryRepository<ParentStudentRelationship>(),
            invitations,
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(parentUserId, "parent@example.com"),
            unitOfWork,
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new DeclineInvitationCommand(invitation.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Declined", result.Value.Status);
        Assert.Equal("StudentParent", result.Value.Kind);
        Assert.Equal(ParentInvitationStatus.Declined, invitation.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithDifferentParentEmail_ReturnsForbidden()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { Email = "parent@example.com" });
        Student student = new(studentUserId);

        StudentParentInvitation invitation = new(
            student.Id,
            "PARENT@EXAMPLE.COM",
            "TEST-HASH",
            DateTimeOffset.UtcNow.AddDays(1));

        invitation.SetPrivateProperty("Student", student);

        InMemoryRepository<StudentParentInvitation> invitations = new(invitation);
        FakeUnitOfWork unitOfWork = new();

        DeclineInvitationCommandHandler handler = new(
            new InMemoryRepository<ParentStudentRelationship>(),
            invitations,
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(parentUserId, "other@example.com"),
            unitOfWork,
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new DeclineInvitationCommand(invitation.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.TopError.Type);
        Assert.Equal("Access.InvitationNotOwned", result.TopError.Code);
        Assert.Equal(ParentInvitationStatus.Pending, invitation.Status);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithLinkInvitation_AllowsParentWithoutMatchingEmail()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { Email = "different@example.com" });
        Student student = new(studentUserId);

        StudentParentInvitation invitation = new(
            student.Id,
            StudentParentInvitationType.Link,
            null,
            "TEST-HASH",
            DateTimeOffset.UtcNow.AddDays(1));
        invitation.SetPrivateProperty("Student", student);

        InMemoryRepository<StudentParentInvitation> invitations = new(invitation);
        FakeUnitOfWork unitOfWork = new();

        DeclineInvitationCommandHandler handler = new(
            new InMemoryRepository<ParentStudentRelationship>(),
            invitations,
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(parentUserId, "different@example.com"),
            unitOfWork,
            TimeProvider.System);

        Result<InvitationResponse> result = await handler.Handle(
            new DeclineInvitationCommand(invitation.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParentInvitationStatus.Declined, invitation.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }
}