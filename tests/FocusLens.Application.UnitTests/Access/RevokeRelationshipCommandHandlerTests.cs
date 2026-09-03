using FocusLens.Application.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.Access;

public class RevokeRelationshipCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithParentOwner_RevokesActiveRelationship()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Parent", parent);
        relationship.SetPrivateProperty("Student", student);
        relationship.Accept();

        var unitOfWork = new FakeUnitOfWork();

        var handler = new RevokeRelationshipCommandHandler(
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(parentUserId),
            unitOfWork);

        Result<Deleted> result = await handler.Handle(
            new RevokeRelationshipCommand(relationship.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RelationshipStatus.Revoked, relationship.Status);
        Assert.NotNull(relationship.RevokedAtUtc);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithStudentOwner_RevokesActiveRelationship()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Parent", parent);
        relationship.SetPrivateProperty("Student", student);
        relationship.Accept();

        var unitOfWork = new FakeUnitOfWork();

        var handler = new RevokeRelationshipCommandHandler(
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(studentUserId),
            unitOfWork);

        Result<Deleted> result = await handler.Handle(
            new RevokeRelationshipCommand(relationship.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RelationshipStatus.Revoked, relationship.Status);
        Assert.NotNull(relationship.RevokedAtUtc);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithUnrelatedUser_ReturnsForbidden()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Parent", parent);
        relationship.SetPrivateProperty("Student", student);
        relationship.Accept();

        var unitOfWork = new FakeUnitOfWork();

        var handler = new RevokeRelationshipCommandHandler(
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(otherUserId),
            unitOfWork);

        Result<Deleted> result = await handler.Handle(
            new RevokeRelationshipCommand(relationship.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.TopError.Type);
        Assert.Equal("Access.RelationshipNotOwned", result.TopError.Code);
        Assert.Equal(RelationshipStatus.Active, relationship.Status);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenRelationshipIsNotActive_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.SetPrivateProperty("Parent", parent);
        relationship.SetPrivateProperty("Student", student);

        var unitOfWork = new FakeUnitOfWork();

        var handler = new RevokeRelationshipCommandHandler(
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(parentUserId),
            unitOfWork);

        Result<Deleted> result = await handler.Handle(
            new RevokeRelationshipCommand(relationship.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.TopError.Type);
        Assert.Equal("Access.InvalidRelationshipState", result.TopError.Code);
        Assert.Equal(RelationshipStatus.Pending, relationship.Status);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithMissingRelationship_ReturnsNotFound()
    {
        Guid parentUserId = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork();

        var handler = new RevokeRelationshipCommandHandler(
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(parentUserId),
            unitOfWork);

        Result<Deleted> result = await handler.Handle(
            new RevokeRelationshipCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("Access.RelationshipNotFound", result.TopError.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }
}
