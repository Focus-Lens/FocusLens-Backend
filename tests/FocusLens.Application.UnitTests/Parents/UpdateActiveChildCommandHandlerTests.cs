using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class UpdateActiveChildCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithActiveRelationship_PersistsRequestedStudent()
    {
        Parent parent = new(Guid.NewGuid());
        Student first = new(Guid.NewGuid());
        Student second = new(Guid.NewGuid());
        ParentStudentRelationship firstRelationship = Active(parent, first);
        ParentStudentRelationship secondRelationship = Active(parent, second);
        FakeUnitOfWork unitOfWork = new();

        UpdateActiveChildCommandHandler handler = CreateHandler(
            parent, [first, second], [firstRelationship, secondRelationship], unitOfWork);

        Result<ActiveChildResponse> result = await handler.Handle(
            new UpdateActiveChildCommand(second.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(second.Id, result.Value.ActiveChildId);
        Assert.Equal(second.Id, parent.ActiveChildId);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithUnrelatedStudent_DoesNotChangePreviousSelection()
    {
        Parent parent = new(Guid.NewGuid());
        Student connected = new(Guid.NewGuid());
        Student unrelated = new(Guid.NewGuid());
        ParentStudentRelationship relationship = Active(parent, connected);
        parent.SetActiveChildId(connected.Id);
        FakeUnitOfWork unitOfWork = new();

        UpdateActiveChildCommandHandler handler = CreateHandler(
            parent, [connected, unrelated], [relationship], unitOfWork);

        Result<ActiveChildResponse> result = await handler.Handle(
            new UpdateActiveChildCommand(unrelated.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal(connected.Id, parent.ActiveChildId);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithSameActiveStudent_IsIdempotent()
    {
        Parent parent = new(Guid.NewGuid());
        Student student = new(Guid.NewGuid());
        ParentStudentRelationship relationship = Active(parent, student);
        parent.SetActiveChildId(student.Id);
        FakeUnitOfWork unitOfWork = new();

        Result<ActiveChildResponse> result = await CreateHandler(parent, [student], [relationship], unitOfWork)
            .Handle(new UpdateActiveChildCommand(student.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(student.Id, result.Value.ActiveChildId);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static UpdateActiveChildCommandHandler CreateHandler(
        Parent parent,
        Student[] students,
        ParentStudentRelationship[] relationships,
        FakeUnitOfWork unitOfWork) => new(
        new InMemoryRepository<Parent>(parent),
        new InMemoryRepository<Student>(students),
        new InMemoryRepository<ParentStudentRelationship>(relationships),
        new FakeCurrentUser(parent.UserId),
        unitOfWork);

    private static ParentStudentRelationship Active(Parent parent, Student student)
    {
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        return relationship;
    }
}
