using System.Security.Claims;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Application.Students;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using ContractStudentGoal = FocusLens.Contracts.Students.StudentGoal;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;

namespace FocusLens.Application.UnitTests.Students;

public class CompleteStudentOnboardingCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenAllPreferencesAreSkipped_ReturnsIncompleteAuthResponse()
    {
        Student student = new(Guid.NewGuid());

        AuthResponse result = (await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(null, null, null)),
            CancellationToken.None)).Value;

        Assert.False(student.IsOnboardingCompleted);
        Assert.True(result.RequiresOnboarding);
        Assert.Equal("incomplete", result.OnboardingStatus);
        Assert.NotNull(result.Tokens);
    }

    [Fact]
    public async Task Handle_WhenAllPreferencesAreProvided_ReturnsCompletedAuthResponse()
    {
        Student student = new(Guid.NewGuid());

        AuthResponse result = (await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    ContractStudentGoal.FocusBetter,
                    ContractStudentGrade.Grade10,
                    [new StudentSubjectRequest(ContractStudentSubjectType.Math, null)])),
            CancellationToken.None)).Value;

        Assert.True(student.IsOnboardingCompleted);
        Assert.False(result.RequiresOnboarding);
        Assert.Equal("completed", result.OnboardingStatus);
        Assert.NotNull(result.Tokens);
    }

    private static CompleteStudentOnboardingCommandHandler CreateHandler(Student student)
        => new(
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(student.UserId),
            new FakeUnitOfWork(),
            new TestIdentityService(student.UserId),
            new TestTokenProvider());

    private sealed class TestIdentityService(Guid userId) : IIdentityService
    {
        private readonly ApplicationUser _user = new()
        {
            Id = userId,
            Email = "student@example.com",
            UserName = "student@example.com",
            FirstName = "Focus",
            LastName = "Student"
        };

        public Task<ApplicationUser?> FindByIdAsync(Guid id) => Task.FromResult<ApplicationUser?>(id == _user.Id ? _user : null);
        public Task<ApplicationUser?> FindByEmailAsync(string email) => Task.FromResult<ApplicationUser?>(null);
        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password) => Task.FromResult(true);
        public Task<bool> IsEmailConfirmedAsync(ApplicationUser user) => Task.FromResult(true);
        public Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(false);
        public Task<IReadOnlyCollection<string>> GetRolesAsync(ApplicationUser user) => Task.FromResult<IReadOnlyCollection<string>>([ApplicationRoles.Student]);
        public Task<bool> IsInRoleAsync(ApplicationUser user, string role) => Task.FromResult(role == ApplicationRoles.Student);
        public Task<IdentityResultSummary> CreateAsync(ApplicationUser user, string password) => Task.FromResult(IdentityResultSummary.Success);
        public Task<IdentityResultSummary> UpdateAsync(ApplicationUser user) => Task.FromResult(IdentityResultSummary.Success);
        public Task<IdentityResultSummary> AddToRoleAsync(ApplicationUser user, string role) => Task.FromResult(IdentityResultSummary.Success);
        public Task<IdentityResultSummary> ConfirmEmailAsync(ApplicationUser user) => Task.FromResult(IdentityResultSummary.Success);
        public Task<IdentityResultSummary> SetPasswordAsync(ApplicationUser user, string newPassword) => Task.FromResult(IdentityResultSummary.Success);
        public Task<IdentityResultSummary> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword) => Task.FromResult(IdentityResultSummary.Success);
        public Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey) => Task.FromResult<ApplicationUser?>(null);
        public Task<IdentityResultSummary> AddLoginAsync(ApplicationUser user, string loginProvider, string providerKey, string displayName) => Task.FromResult(IdentityResultSummary.Success);
    }

    private sealed class TestTokenProvider : ITokenProvider
    {
        public Task<TokenPair> CreateTokenPairAsync(ApplicationUser user, CancellationToken cancellationToken = default)
            => Task.FromResult(new TokenPair("access-token", DateTimeOffset.UtcNow.AddMinutes(15), "refresh-token", DateTimeOffset.UtcNow.AddDays(30)));
        public Task<(string Token, DateTimeOffset ExpiresOnUtc)> CreateOnboardingTokenAsync(ApplicationUser user)
            => Task.FromResult(("registration-token", DateTimeOffset.UtcNow.AddMinutes(15)));
        public ClaimsPrincipal GetPrincipalFromExpiredToken(string accessToken) => new();
        public string GenerateRefreshToken() => "refresh-token";
        public Task<RefreshToken> PersistRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult<RefreshToken?>(null);
        public Task<TokenPair?> RotateRefreshTokenAsync(ApplicationUser user, string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult<TokenPair?>(null);
        public Task<bool> RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
