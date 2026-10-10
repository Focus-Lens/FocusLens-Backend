using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts;
using FocusLens.Domain;
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
            new FakeCurrentUser(parentUserId));

        ParentResponse? result = await handler.Handle(
            new GetMyParentQuery(),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(parent.Id, result.Id);
        Assert.Equal(parent.UserId, result.UserId);
        Assert.Equal("Mona", result.FirstName);
        Assert.Equal("Hassan", result.LastName);
    }
}