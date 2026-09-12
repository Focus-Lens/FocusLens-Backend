using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Access;

public class GetMyStudentsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithActiveRelationship_ReturnsStudentSummary()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);

        Student student = new(studentUserId);
        student.SetPrivateProperty("User", new ApplicationUser { FirstName = "Karim", LastName = "Mahmoud" });

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);
        relationship.Accept();

        GetMyStudentsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(parentUserId));

        IReadOnlyList<ParentStudentSummaryResponse> result = await handler.Handle(
            new GetMyStudentsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(student.Id, result[0].Id);
        Assert.Equal(student.PreferredName, result[0].PreferredName);
        Assert.Equal("Karim", result[0].FirstName);
        Assert.Equal("Mahmoud", result[0].LastName);
        Assert.Null(result[0].Grade);
        Assert.Empty(result[0].Subjects);
        Assert.Equal(student.IsOnboardingCompleted, result[0].OnboardingCompleted);
    }

    [Fact]
    public async Task Handle_WithPendingRelationship_ReturnsEmptyList()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);

        Student student = new(studentUserId);
        student.SetPrivateProperty("User", new ApplicationUser { FirstName = "Karim", LastName = "Mahmoud" });

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        GetMyStudentsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(parentUserId));

        IReadOnlyList<ParentStudentSummaryResponse> result = await handler.Handle(
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
        linkedStudent.SetPrivateProperty("User",
            new ApplicationUser { FirstName = "Linked", LastName = "Student" });

        Student unrelatedStudent = new(Guid.NewGuid());
        unrelatedStudent.SetPrivateProperty("User",
            new ApplicationUser { FirstName = "Unrelated", LastName = "Student" });

        ParentStudentRelationship relationship =
            new(parent.Id, linkedStudent.Id);
        relationship.Accept();

        GetMyStudentsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(linkedStudent, unrelatedStudent),
            new FakeCurrentUser(parentUserId));

        IReadOnlyList<ParentStudentSummaryResponse> result = await handler.Handle(
            new GetMyStudentsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(linkedStudent.Id, result[0].Id);
        Assert.Equal("Linked", result[0].FirstName);
        Assert.Equal("Student", result[0].LastName);
    }

    [Fact]
    public async Task Handle_WhenParentDoesNotExist_ReturnsEmptyList()
    {
        Student student = new(Guid.NewGuid());

        GetMyStudentsQueryHandler handler = new(
            new InMemoryRepository<Parent>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(Guid.NewGuid()));

        IReadOnlyList<ParentStudentSummaryResponse> result = await handler.Handle(
            new GetMyStudentsQuery(),
            CancellationToken.None);

        Assert.Empty(result);
    }
}