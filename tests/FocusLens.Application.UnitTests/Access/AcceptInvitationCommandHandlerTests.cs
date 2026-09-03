using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.Access;

public class AcceptInvitationCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithCorrectStudent_ActivatesInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Student", student);

        var relationshipRepository =
            new InMemoryRepository<ParentStudentRelationship>(relationship);
        var unitOfWork = new FakeUnitOfWork();

        var handler = new AcceptInvitationCommandHandler(
            relationshipRepository,
            new FakeCurrentUser(studentUserId),
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new AcceptInvitationCommand(relationship.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value.Status);
        Assert.Equal(RelationshipStatus.Active, relationship.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithDifferentStudent_ReturnsForbidden()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();
        Guid otherStudentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Student", student);

        var unitOfWork = new FakeUnitOfWork();

        var handler = new AcceptInvitationCommandHandler(
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(otherStudentUserId),
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new AcceptInvitationCommand(relationship.Id),
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
        var unitOfWork = new FakeUnitOfWork();

        var handler = new AcceptInvitationCommandHandler(
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(studentUserId),
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new AcceptInvitationCommand(Guid.NewGuid()),
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
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Student", student);
        relationship.Accept();

        var unitOfWork = new FakeUnitOfWork();

        var handler = new AcceptInvitationCommandHandler(
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(studentUserId),
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new AcceptInvitationCommand(relationship.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.TopError.Type);
        Assert.Equal("Access.InvalidInvitationState", result.TopError.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }
}
