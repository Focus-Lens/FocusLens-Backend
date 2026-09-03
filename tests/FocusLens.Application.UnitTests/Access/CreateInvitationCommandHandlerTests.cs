using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public class CreateInvitationCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidParentAndStudent_CreatesPendingInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ApplicationUser studentUser = new()
        {
            Email = "student@example.com"
        };
        student.SetPrivateProperty("User", studentUser);

        var parentRepository = new InMemoryRepository<Parent>(parent);
        var studentRepository = new InMemoryRepository<Student>(student);
        var relationshipRepository = new InMemoryRepository<ParentStudentRelationship>();
        var currentUser = new FakeCurrentUser(parentUserId);
        var unitOfWork = new FakeUnitOfWork();

        var handler = new CreateInvitationCommandHandler(
            parentRepository,
            studentRepository,
            relationshipRepository,
            currentUser,
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("student@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(parent.Id, result.Value.ParentId);
        Assert.Equal(student.Id, result.Value.StudentId);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenStudentDoesNotExist_ReturnsNotFound()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        var handler = new CreateInvitationCommandHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork());

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("missing@example.com")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("Access.StudentNotFound", result.TopError.Code);
    }

    [Fact]
    public async Task Handle_WithDuplicateRelationship_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ApplicationUser studentUser = new()
        {
            Email = "student@example.com"
        };
        student.SetPrivateProperty("User", studentUser);

        ParentStudentRelationship existingRelationship =
            new(parent.Id, student.Id);

        var unitOfWork = new FakeUnitOfWork();

        var handler = new CreateInvitationCommandHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(existingRelationship),
            new FakeCurrentUser(parentUserId),
            unitOfWork);

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("student@example.com")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.TopError.Type);
        Assert.Equal("Access.RelationshipExists", result.TopError.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_ReturnsValidationError()
    {
        var handler = new CreateInvitationCommandHandler(
            new InMemoryRepository<Parent>(),
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeUnitOfWork());

        Result<InvitationResponse> result = await handler.Handle(
            new CreateInvitationCommand(
                new CreateInvitationRequest("   ")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.TopError.Type);
        Assert.Equal("Access.StudentEmailRequired", result.TopError.Code);
    }
}
