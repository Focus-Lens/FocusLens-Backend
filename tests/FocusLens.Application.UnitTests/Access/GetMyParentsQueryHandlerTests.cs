using FocusLens.Application.Access;
using FocusLens.Contracts.Parents;
using FocusLens.Domain;
using FocusLens.Domain.Access;

namespace FocusLens.Application.UnitTests.Access;

public class GetMyParentsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithActiveRelationship_ReturnsParent()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);
        relationship.Accept();

        var handler = new GetMyParentsQueryHandler(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(studentUserId));

        IReadOnlyList<ParentResponse> result = await handler.Handle(
            new GetMyParentsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(parent.Id, result[0].Id);
        Assert.Equal(parent.UserId, result[0].UserId);
    }

    [Fact]
    public async Task Handle_WithPendingRelationship_ReturnsEmptyList()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        var handler = new GetMyParentsQueryHandler(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(studentUserId));

        IReadOnlyList<ParentResponse> result = await handler.Handle(
            new GetMyParentsQuery(),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithUnrelatedParent_ReturnsOnlyLinkedParent()
    {
        Guid studentUserId = Guid.NewGuid();

        Student student = new(studentUserId);
        Parent linkedParent = new(Guid.NewGuid());
        Parent unrelatedParent = new(Guid.NewGuid());

        ParentStudentRelationship relationship =
            new(linkedParent.Id, student.Id);
        relationship.Accept();

        var handler = new GetMyParentsQueryHandler(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Parent>(linkedParent, unrelatedParent),
            new FakeCurrentUser(studentUserId));

        IReadOnlyList<ParentResponse> result = await handler.Handle(
            new GetMyParentsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(linkedParent.Id, result[0].Id);
    }

    [Fact]
    public async Task Handle_WhenStudentDoesNotExist_ReturnsEmptyList()
    {
        Parent parent = new(Guid.NewGuid());

        var handler = new GetMyParentsQueryHandler(
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(Guid.NewGuid()));

        IReadOnlyList<ParentResponse> result = await handler.Handle(
            new GetMyParentsQuery(),
            CancellationToken.None);

        Assert.Empty(result);
    }
}
