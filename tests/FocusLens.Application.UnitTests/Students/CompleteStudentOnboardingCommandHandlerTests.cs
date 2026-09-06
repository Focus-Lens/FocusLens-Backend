using System.Security.Claims;

using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Application.Students;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using DomainStudentSubjectType = FocusLens.Domain.Students.StudentSubjectType;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ContractStudentGoal = FocusLens.Contracts.Students.StudentGoal;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;

namespace FocusLens.Application.UnitTests.Students;

public class CompleteStudentOnboardingCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenAllOnboardingInformationIsPresent_ReturnsTokensAndCompletedStatus()
    {
        Student student = new(Guid.NewGuid());
        var result = await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    ContractStudentGoal.FocusBetter,
                    ContractStudentGrade.Grade10,
                    [new StudentSubjectRequest(ContractStudentSubjectType.Math, null)])),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Tokens);
        Assert.Equal("access-token", result.Value.Tokens.AccessToken);
        Assert.Equal("refresh-token", result.Value.Tokens.RefreshToken);
        Assert.Equal("completed", result.Value.OnboardingStatus);
        Assert.False(result.Value.RequiresOnboarding);
        Assert.True(student.IsOnboardingCompleted);
    }

    [Fact]
    public async Task Handle_WhenGoalIsMissing_ReturnsTokensAndIncompleteStatus()
    {
        Student student = new(Guid.NewGuid());
        var result = await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    null,
                    ContractStudentGrade.Grade10,
                    [new StudentSubjectRequest(ContractStudentSubjectType.Math, null)])),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Tokens);
        Assert.Equal("incomplete", result.Value.OnboardingStatus);
        Assert.True(result.Value.RequiresOnboarding);
        Assert.False(student.IsOnboardingCompleted);
    }

    [Fact]
    public async Task Handle_WhenGradeIsMissing_ReturnsTokensAndIncompleteStatus()
    {
        Student student = new(Guid.NewGuid());
        var result = await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    ContractStudentGoal.FocusBetter,
                    null,
                    [new StudentSubjectRequest(ContractStudentSubjectType.Math, null)])),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Tokens);
        Assert.Equal("incomplete", result.Value.OnboardingStatus);
        Assert.True(result.Value.RequiresOnboarding);
        Assert.False(student.IsOnboardingCompleted);
    }

    [Theory]
    [MemberData(nameof(MissingOrEmptySubjects))]
    public async Task Handle_WhenSubjectsAreMissingOrEmpty_ReturnsTokensAndIncompleteStatus(
        IReadOnlyCollection<StudentSubjectRequest>? subjects)
    {
        Student student = new(Guid.NewGuid());
        var result = await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    ContractStudentGoal.FocusBetter,
                    ContractStudentGrade.Grade10,
                    subjects!)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Tokens);
        Assert.Equal("incomplete", result.Value.OnboardingStatus);
        Assert.True(result.Value.RequiresOnboarding);
        Assert.False(student.IsOnboardingCompleted);
        Assert.Empty(student.Subjects);
    }

    [Fact]
    public async Task Handle_WhenMultipleFieldsAreMissing_ReturnsTokensAndIncompleteStatus()
    {
        Student student = new(Guid.NewGuid());
        var result = await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    null,
                    null,
                    [])),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Tokens);
        Assert.Equal("access-token", result.Value.Tokens.AccessToken);
        Assert.Equal("refresh-token", result.Value.Tokens.RefreshToken);
        Assert.Equal("incomplete", result.Value.OnboardingStatus);
        Assert.True(result.Value.RequiresOnboarding);
        Assert.False(student.IsOnboardingCompleted);
        Assert.Empty(student.Subjects);
    }

    [Fact]
    public void Validator_WhenAllOptionalFieldsAreMissing_AllowsRequest()
    {
        var validator = new CompleteStudentOnboardingCommandValidator();

        var result = validator.Validate(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    null,
                    null,
                    null!)));

        Assert.True(result.IsValid);
    }

    public static TheoryData<IReadOnlyCollection<StudentSubjectRequest>?> MissingOrEmptySubjects()
        => new()
        {
            null,
            Array.Empty<StudentSubjectRequest>()
        };

    private static CompleteStudentOnboardingCommandHandler CreateHandler(Student student)
    {
        ApplicationUser user = new()
        {
            Id = student.UserId,
            Email = "student@example.com",
            UserName = "student@example.com",
            FirstName = "Focus",
            LastName = "Student"
        };

        return new CompleteStudentOnboardingCommandHandler(
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(student.UserId),
            new FakeUnitOfWork(),
            new FakeIdentityService(user),
            new FakeTokenProvider());
    }

    private sealed class FakeIdentityService(ApplicationUser user) : IIdentityService
    {
        public Task<ApplicationUser?> FindByIdAsync(Guid userId)
            => Task.FromResult(userId == user.Id ? user : null);

        public Task<ApplicationUser?> FindByEmailAsync(string email)
            => Task.FromResult<ApplicationUser?>(null);

        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
            => Task.FromResult(true);

        public Task<bool> IsEmailConfirmedAsync(ApplicationUser user)
            => Task.FromResult(true);

        public Task<bool> IsLockedOutAsync(ApplicationUser user)
            => Task.FromResult(false);

        public Task<IReadOnlyCollection<string>> GetRolesAsync(ApplicationUser user)
            => Task.FromResult<IReadOnlyCollection<string>>([ApplicationRoles.Student]);

        public Task<bool> IsInRoleAsync(ApplicationUser user, string role)
            => Task.FromResult(role == ApplicationRoles.Student);

        public Task<IdentityResultSummary> CreateAsync(ApplicationUser user, string password)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> UpdateAsync(ApplicationUser user)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> AddToRoleAsync(ApplicationUser user, string role)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> ConfirmEmailAsync(ApplicationUser user)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> SetPasswordAsync(
            ApplicationUser user,
            string newPassword)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> ChangePasswordAsync(
            ApplicationUser user,
            string currentPassword,
            string newPassword)
            => Task.FromResult(IdentityResultSummary.Success);

        public Task<ApplicationUser?> FindByLoginAsync(
            string loginProvider,
            string providerKey)
            => Task.FromResult<ApplicationUser?>(null);

        public Task<IdentityResultSummary> AddLoginAsync(
            ApplicationUser user,
            string loginProvider,
            string providerKey,
            string displayName)
            => Task.FromResult(IdentityResultSummary.Success);
    }

    private sealed class FakeTokenProvider : ITokenProvider
    {
        public Task<TokenPair> CreateTokenPairAsync(
            ApplicationUser user,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TokenPair(
                "access-token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

        public Task<(string Token, DateTimeOffset ExpiresOnUtc)> CreateOnboardingTokenAsync(
            ApplicationUser user)
            => Task.FromResult(("registration-token", DateTimeOffset.UtcNow.AddMinutes(15)));

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string accessToken)
            => new();

        public string GenerateRefreshToken() => "refresh-token";

        public Task<RefreshToken> PersistRefreshTokenAsync(
            Guid userId,
            string refreshToken,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RefreshToken?> GetRefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
            => Task.FromResult<RefreshToken?>(null);

        public Task<TokenPair?> RotateRefreshTokenAsync(
            ApplicationUser user,
            string refreshToken,
            CancellationToken cancellationToken = default)
            => Task.FromResult<TokenPair?>(null);

        public Task<bool> RevokeRefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task RevokeAllRefreshTokensAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
