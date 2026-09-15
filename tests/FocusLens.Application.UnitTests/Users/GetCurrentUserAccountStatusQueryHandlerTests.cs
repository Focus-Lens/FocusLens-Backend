using FocusLens.Application.Features.Users.Queries.GetCurrentUserAccountStatus;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;

namespace FocusLens.Application.UnitTests.Users;

public sealed class GetCurrentUserAccountStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithCurrentParent_ReturnsIdentityAndChildRelationshipStatuses()
    {
        ApplicationUser user = new()
        {
            EmailConfirmed = true,
            FirstName = "Mona",
            LastName = "Hassan"
        };
        Parent parent = new(user.Id);
        Student activeChild = CreateStudent("Lina", "Hassan", "Lulu");
        Student pendingChild = CreateStudent("Omar", "Hassan");
        ParentStudentRelationship activeRelationship = new(parent.Id, activeChild.Id);
        activeRelationship.Accept();
        ParentStudentRelationship pendingRelationship = new(parent.Id, pendingChild.Id);

        GetCurrentUserAccountStatusQueryHandler handler = new(
            new FakeCurrentUser(user.Id),
            new TestIdentityService(user),
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(
                activeRelationship,
                pendingRelationship),
            new InMemoryRepository<Student>(activeChild, pendingChild));

        var result = await handler.Handle(
            new GetCurrentUserAccountStatusQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.EmailConfirmed);
        Assert.Equal("Verified", result.Value.EmailVerificationStatus);
        Assert.Equal("Active", result.Value.AccountStatus);
        Assert.Collection(
            result.Value.ChildRelationships,
            relationship =>
            {
                Assert.Equal(activeRelationship.Id, relationship.RelationshipId);
                Assert.Equal(activeChild.Id, relationship.ChildId);
                Assert.Equal("Lulu", relationship.ChildName);
                Assert.Equal("Active", relationship.RelationshipStatus);
            },
            relationship =>
            {
                Assert.Equal(pendingRelationship.Id, relationship.RelationshipId);
                Assert.Equal(pendingChild.Id, relationship.ChildId);
                Assert.Equal("Omar Hassan", relationship.ChildName);
                Assert.Equal("Pending", relationship.RelationshipStatus);
            });
    }

    [Fact]
    public async Task Handle_WithStudentUser_ReturnsIdentityStatusWithoutChildRelationships()
    {
        ApplicationUser user = new()
        {
            EmailConfirmed = false,
            IsDisabled = true
        };

        GetCurrentUserAccountStatusQueryHandler handler = new(
            new FakeCurrentUser(user.Id),
            new TestIdentityService(user),
            new InMemoryRepository<Parent>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<Student>());

        var result = await handler.Handle(
            new GetCurrentUserAccountStatusQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.EmailConfirmed);
        Assert.Equal("Unverified", result.Value.EmailVerificationStatus);
        Assert.True(result.Value.IsDisabled);
        Assert.Equal("Disabled", result.Value.AccountStatus);
        Assert.Empty(result.Value.ChildRelationships);
    }

    private static Student CreateStudent(
        string firstName,
        string lastName,
        string? preferredName = null)
    {
        ApplicationUser user = new()
        {
            FirstName = firstName,
            LastName = lastName
        };
        Student student = new(user.Id);
        student.SetPrivateProperty("User", user);
        student.SetPreferredName(preferredName);

        return student;
    }

    private sealed class TestIdentityService(ApplicationUser user) : IIdentityService
    {
        public Task<ApplicationUser?> FindByIdAsync(Guid userId)
            => Task.FromResult(user.Id == userId ? user : null);

        public Task<ApplicationUser?> FindByEmailAsync(string email)
            => Task.FromResult<ApplicationUser?>(null);

        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
            => Task.FromResult(false);

        public Task<bool> IsEmailConfirmedAsync(ApplicationUser user)
            => Task.FromResult(user.EmailConfirmed);

        public Task<bool> IsLockedOutAsync(ApplicationUser user)
            => Task.FromResult(false);

        public Task<IReadOnlyCollection<string>> GetRolesAsync(ApplicationUser user)
            => Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<bool> IsInRoleAsync(ApplicationUser user, string role)
            => Task.FromResult(false);

        public Task<IdentityResultSummary> CreateAsync(ApplicationUser user, string password)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> UpdateAsync(ApplicationUser user)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> AddToRoleAsync(ApplicationUser user, string role)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> ConfirmEmailAsync(ApplicationUser user)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> SetPasswordAsync(ApplicationUser user, string newPassword)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> ChangePasswordAsync(
            ApplicationUser user,
            string currentPassword,
            string newPassword)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey)
            => Task.FromResult<ApplicationUser?>(null);

        public Task<IdentityResultSummary> AddLoginAsync(
            ApplicationUser user,
            string loginProvider,
            string providerKey,
            string displayName)
            => Task.FromResult(IdentityResultSummary.Success);
    }
}
