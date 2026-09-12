using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public class GetMyParentsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithActiveRelationship_ReturnsParentSummary()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User",
            new ApplicationUser { FirstName = "Ahmed", LastName = "Mahmoud", Email = "parent@example.com" });

        Student student = new(studentUserId);

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);
        relationship.Accept();

        GetMyParentsQueryHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(studentUserId));

        IReadOnlyList<StudentParentSummaryResponse> result = await handler.Handle(
            new GetMyParentsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(parent.Id, result[0].Id);
        Assert.Equal("Ahmed", result[0].FirstName);
        Assert.Equal("Mahmoud", result[0].LastName);
        Assert.Equal("parent@example.com", result[0].Email);
    }

    [Fact]
    public async Task Handle_WithPendingRelationship_ReturnsEmptyList()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User",
            new ApplicationUser { FirstName = "Ahmed", LastName = "Mahmoud", Email = "parent@example.com" });

        Student student = new(studentUserId);

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        GetMyParentsQueryHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(studentUserId));

        IReadOnlyList<StudentParentSummaryResponse> result = await handler.Handle(
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
        linkedParent.SetPrivateProperty("User",
            new ApplicationUser { FirstName = "Linked", LastName = "Parent", Email = "linked@example.com" });

        Parent unrelatedParent = new(Guid.NewGuid());
        unrelatedParent.SetPrivateProperty("User",
            new ApplicationUser { FirstName = "Unrelated", LastName = "Parent", Email = "unrelated@example.com" });

        ParentStudentRelationship relationship =
            new(linkedParent.Id, student.Id);
        relationship.Accept();

        GetMyParentsQueryHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Parent>(linkedParent, unrelatedParent),
            new FakeCurrentUser(studentUserId));

        IReadOnlyList<StudentParentSummaryResponse> result = await handler.Handle(
            new GetMyParentsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(linkedParent.Id, result[0].Id);
        Assert.Equal("Linked", result[0].FirstName);
        Assert.Equal("Parent", result[0].LastName);
        Assert.Equal("linked@example.com", result[0].Email);
    }

    [Fact]
    public async Task Handle_WhenStudentDoesNotExist_ReturnsEmptyList()
    {
        Parent parent = new(Guid.NewGuid());
        parent.SetPrivateProperty("User",
            new ApplicationUser { FirstName = "Ahmed", LastName = "Mahmoud", Email = "parent@example.com" });

        GetMyParentsQueryHandler handler = new(
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(Guid.NewGuid()));

        IReadOnlyList<StudentParentSummaryResponse> result = await handler.Handle(
            new GetMyParentsQuery(),
            CancellationToken.None);

        Assert.Empty(result);
    }
}