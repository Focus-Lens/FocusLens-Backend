using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class GetMyParentQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithCurrentParent_ReturnsParentProfileWithName()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        parent.SetPrivateProperty(
            "User",
            new ApplicationUser { FirstName = "Mona", LastName = "Hassan" });

        GetMyParentQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeUnitOfWork(),
            new FakeCurrentUser(parentUserId));

        ParentResponse? result = await handler.Handle(
            new GetMyParentQuery(),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(parent.Id, result.Id);
        Assert.Equal(parent.UserId, result.UserId);
        Assert.Equal("Mona", result.FirstName);
        Assert.Equal("Hassan", result.LastName);
        Assert.Null(result.ActiveChildId);
    }

    [Fact]
    public async Task Handle_WithNoSavedSelection_UsesDeterministicActiveRelationship()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { FirstName = "Mona", LastName = "Hassan" });
        Student first = new(Guid.NewGuid());
        Student second = new(Guid.NewGuid());
        ParentStudentRelationship firstRelationship = new(parent.Id, first.Id);
        ParentStudentRelationship secondRelationship = new(parent.Id, second.Id);
        firstRelationship.Accept();
        secondRelationship.Accept();
        firstRelationship.CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        secondRelationship.CreatedAtUtc = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        FakeUnitOfWork unitOfWork = new();

        ParentResponse? result = await new GetMyParentQueryHandler(
                new InMemoryRepository<Parent>(parent),
                new InMemoryRepository<ParentStudentRelationship>(firstRelationship, secondRelationship),
                unitOfWork,
                new FakeCurrentUser(parentUserId))
            .Handle(new GetMyParentQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(first.Id, result.ActiveChildId);
        Assert.Equal(first.Id, parent.ActiveChildId);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }
}
