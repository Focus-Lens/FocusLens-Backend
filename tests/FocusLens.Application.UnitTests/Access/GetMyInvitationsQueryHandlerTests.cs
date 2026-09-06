using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public class GetMyInvitationsQueryHandlerTests
{
    [Fact]
    public async Task Handle_AsParent_ReturnsMyInvitations()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser
        {
            Email = "parent@example.com"
        });
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        var handler = new GetMyInvitationsQueryHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(parentUserId));

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(relationship.Id, result[0].Id);
        Assert.Equal(parent.Id, result[0].ParentId);
        Assert.Equal(student.Id, result[0].StudentId);
        Assert.Equal("parent@example.com", result[0].ParentEmail);
        Assert.Equal("Pending", result[0].Status);
    }

    [Fact]
    public async Task Handle_AsStudent_ReturnsReceivedInvitations()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser
        {
            Email = "parent@example.com"
        });
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        var handler = new GetMyInvitationsQueryHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(studentUserId));

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(relationship.Id, result[0].Id);
        Assert.Equal(parent.Id, result[0].ParentId);
        Assert.Equal(student.Id, result[0].StudentId);
        Assert.Equal("parent@example.com", result[0].ParentEmail);
    }

    [Fact]
    public async Task Handle_WithUnrelatedUser_ReturnsEmptyList()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        var handler = new GetMyInvitationsQueryHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(otherUserId));

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithEmptyCurrentUser_ReturnsEmptyList()
    {
        Parent parent = new(Guid.NewGuid());
        Student student = new(Guid.NewGuid());
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        var handler = new GetMyInvitationsQueryHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(Guid.Empty));

        IReadOnlyList<InvitationResponse> result = await handler.Handle(
            new GetMyInvitationsQuery(),
            CancellationToken.None);

        Assert.Empty(result);
    }
}
