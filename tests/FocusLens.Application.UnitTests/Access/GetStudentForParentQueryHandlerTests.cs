using FocusLens.Application.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;
using ContractStudentGoal = FocusLens.Contracts.Students.StudentGoal;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using StudentGoal = FocusLens.Domain.Students.StudentGoal;
using StudentGrade = FocusLens.Domain.Students.StudentGrade;
using StudentSubjectType = FocusLens.Domain.Students.StudentSubjectType;

namespace FocusLens.Application.UnitTests.Access;

public class GetStudentForParentQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithActiveRelationship_ReturnsStudentDetails()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid studentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(studentUserId);

        student.SetPrivateProperty(
            "User",
            new ApplicationUser
            {
                Id = studentUserId,
                Email = "student@example.com",
                UserName = "student@example.com",
                FirstName = "Youssef",
                LastName = "Mahmoud"
            });

        student.CompleteOnboarding(
            new[] { StudentGoal.FocusBetter },
            StudentGrade.Grade10,
            [
                StudentSubject.Predefined(StudentSubjectType.Math),
                StudentSubject.Custom("Economics")
            ]);

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.Accept();

        GetStudentForParentQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(parentUserId));

        StudentDetailsResponse? result = await handler.Handle(
            new GetStudentForParentQuery(student.Id),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(student.Id, result.Id);
        Assert.Equal(student.UserId, result.UserId);
        Assert.Single(result.Goals);
        Assert.Equal(ContractStudentGoal.FocusBetter, result.Goals.Single());
        Assert.Equal(ContractStudentGrade.Grade10, result.Grade);
        Assert.True(result.OnboardingCompleted);
        Assert.Equal(2, result.Subjects.Count);
        Assert.Contains(
            result.Subjects,
            subject => subject.Type == ContractStudentSubjectType.Math &&
                       subject.CustomName is null);
        Assert.Contains(
            result.Subjects,
            subject => subject.Type == ContractStudentSubjectType.Other &&
                       subject.CustomName == "Economics");
    }

    [Fact]
    public async Task Handle_WithPendingRelationship_ReturnsNull()
    {
        Guid parentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(Guid.NewGuid());

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        GetStudentForParentQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(parentUserId));

        StudentDetailsResponse? result = await handler.Handle(
            new GetStudentForParentQuery(student.Id),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithRevokedRelationship_ReturnsNull()
    {
        Guid parentUserId = Guid.NewGuid();

        Parent parent = new(parentUserId);
        Student student = new(Guid.NewGuid());

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.Accept();
        relationship.Revoke();

        GetStudentForParentQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(parentUserId));

        StudentDetailsResponse? result = await handler.Handle(
            new GetStudentForParentQuery(student.Id),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithUnrelatedParent_ReturnsNull()
    {
        Parent parent = new(Guid.NewGuid());
        Student student = new(Guid.NewGuid());

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationship.Accept();

        GetStudentForParentQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(Guid.NewGuid()));

        StudentDetailsResponse? result = await handler.Handle(
            new GetStudentForParentQuery(student.Id),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WhenRelationshipReferencesMissingStudent_ReturnsNull()
    {
        Guid parentUserId = Guid.NewGuid();
        Guid missingStudentId = Guid.NewGuid();

        Parent parent = new(parentUserId);

        ParentStudentRelationship relationship =
            new(parent.Id, missingStudentId);

        relationship.Accept();

        GetStudentForParentQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(),
            new FakeCurrentUser(parentUserId));

        StudentDetailsResponse? result = await handler.Handle(
            new GetStudentForParentQuery(missingStudentId),
            CancellationToken.None);

        Assert.Null(result);
    }
}