using FocusLens.Application.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;

namespace FocusLens.Application.UnitTests.Access;

public class GetMyStudentsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithActiveRelationship_ReturnsStudent()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);
        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);
        relationship.Accept();

        var handler = new GetMyStudentsQueryHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(parentUserId));

        IReadOnlyList<StudentResponse> result = await handler.Handle(
            new GetMyStudentsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(student.Id, result[0].Id);
        Assert.Equal(student.UserId, result[0].UserId);
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

        var handler = new GetMyStudentsQueryHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(parentUserId));

        IReadOnlyList<StudentResponse> result = await handler.Handle(
            new GetMyStudentsQuery(),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithUnrelatedStudent_ReturnsOnlyLinkedStudent()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        Student linkedStudent = new(Guid.NewGuid());
        Student unrelatedStudent = new(Guid.NewGuid());

        ParentStudentRelationship relationship =
            new(parent.Id, linkedStudent.Id);
        relationship.Accept();

        var handler = new GetMyStudentsQueryHandler(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(linkedStudent, unrelatedStudent),
            new FakeCurrentUser(parentUserId));

        IReadOnlyList<StudentResponse> result = await handler.Handle(
            new GetMyStudentsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(linkedStudent.Id, result[0].Id);
    }

    [Fact]
    public async Task Handle_WhenParentDoesNotExist_ReturnsEmptyList()
    {
        Student student = new(Guid.NewGuid());
        var handler = new GetMyStudentsQueryHandler(
            new InMemoryRepository<Parent>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(Guid.NewGuid()));

        IReadOnlyList<StudentResponse> result = await handler.Handle(
            new GetMyStudentsQuery(),
            CancellationToken.None);

        Assert.Empty(result);
    }
}
