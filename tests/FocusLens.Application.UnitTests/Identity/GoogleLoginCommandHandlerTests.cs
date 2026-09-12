using System.Security.Claims;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Models;
using FocusLens.Application.Features.Identity.Commands.GoogleLogin;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using FocusLens.Domain.Students;

namespace FocusLens.Application.UnitTests.Identity;

public class GoogleLoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenGoogleUserIsNew_CreatesAccountAndStudentAndReturnsRegistrationToken()
    {
        TestFixture fixture = CreateFixture();

        Result<AuthResponse> result = await fixture.Handler.Handle(
            new GoogleLoginCommand("valid-google-token"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Tokens);
        Assert.True(result.Value.RequiresOnboarding);
        Assert.Equal("registration-token", result.Value.RegistrationToken);
        Assert.Single(fixture.Identity.Users);
        Assert.Single(await fixture.Students.GetAllAsync());
    }

    [Fact]
    public async Task Handle_WhenExistingGoogleUserHasIncompleteOnboarding_ReturnsTokensAndIncompleteStatus()
    {
        TestFixture fixture = CreateFixture();
        ApplicationUser user = fixture.Identity.AddExistingGoogleStudent(
            "google-subject",
            "student@example.com");
        fixture.Students.Add(new Student(user.Id));

        Result<AuthResponse> result = await fixture.Handler.Handle(
            new GoogleLoginCommand("valid-google-token"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Tokens);
        Assert.Equal("access-token", result.Value.Tokens.AccessToken);
        Assert.Equal("refresh-token", result.Value.Tokens.RefreshToken);
        Assert.True(result.Value.RequiresOnboarding);
        Assert.Null(result.Value.RegistrationToken);
        Assert.Equal("incomplete", result.Value.OnboardingStatus);
    }

    [Fact]
    public async Task Handle_WhenExistingGoogleUserHasCompletedOnboarding_ReturnsTokensAndCompletedStatus()
    {
        TestFixture fixture = CreateFixture();
        ApplicationUser user = fixture.Identity.AddExistingGoogleStudent(
            "google-subject",
            "student@example.com");
        Student student = new(user.Id);
        student.CompleteOnboarding(
            new[] { StudentGoal.FocusBetter },
            StudentGrade.Grade10,
            [StudentSubject.Predefined(StudentSubjectType.Math)]);
        fixture.Students.Add(student);

        Result<AuthResponse> result = await fixture.Handler.Handle(
            new GoogleLoginCommand("valid-google-token"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Tokens);
        Assert.False(result.Value.RequiresOnboarding);
        Assert.Equal("completed", result.Value.OnboardingStatus);
    }

    [Fact]
    public async Task Handle_WhenSameGoogleAccountContinuesAgain_DoesNotCreateDuplicates()
    {
        TestFixture fixture = CreateFixture();

        await fixture.Handler.Handle(
            new GoogleLoginCommand("valid-google-token"),
            CancellationToken.None);
        await fixture.Handler.Handle(
            new GoogleLoginCommand("valid-google-token"),
            CancellationToken.None);

        Assert.Single(fixture.Identity.Users);
        Assert.Single(await fixture.Students.GetAllAsync());
    }

    private static TestFixture CreateFixture()
    {
        FakeGoogleTokenValidator googleTokenValidator = new(
            new GoogleUserInfo(
                "google-subject",
                "student@example.com",
                "Focus",
                "Student",
                true));
        FakeIdentityService identity = new();
        FakeTokenProvider tokenProvider = new();
        InMemoryRepository<Student> students = new();
        FakeUnitOfWork unitOfWork = new();

        return new TestFixture(
            new GoogleLoginCommandHandler(
                googleTokenValidator,
                identity,
                tokenProvider,
                students,
                unitOfWork),
            identity,
            students);
    }

    private sealed record TestFixture(
        GoogleLoginCommandHandler Handler,
        FakeIdentityService Identity,
        InMemoryRepository<Student> Students);

    private sealed class FakeGoogleTokenValidator(GoogleUserInfo googleUser)
        : IGoogleTokenValidator
    {
        public Task<GoogleUserInfo?> ValidateAsync(
            string idToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GoogleUserInfo?>(googleUser);
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Dictionary<Guid, List<string>> _roles = [];
        private readonly Dictionary<Guid, ApplicationUser> _users = [];
        private readonly Dictionary<string, Guid> _usersByEmail = [];
        private readonly Dictionary<string, Guid> _usersByLogin = [];

        public IReadOnlyCollection<ApplicationUser> Users => _users.Values;

        public Task<ApplicationUser?> FindByIdAsync(Guid userId) => Task.FromResult(_users.GetValueOrDefault(userId));

        public Task<ApplicationUser?> FindByEmailAsync(string email)
        {
            return Task.FromResult(
                _usersByEmail.TryGetValue(email, out Guid userId)
                    ? _users[userId]
                    : null);
        }

        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password) => Task.FromResult(true);

        public Task<bool> IsEmailConfirmedAsync(ApplicationUser user) => Task.FromResult(user.EmailConfirmed);

        public Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(false);

        public Task<IReadOnlyCollection<string>> GetRolesAsync(ApplicationUser user)
        {
            return Task.FromResult<IReadOnlyCollection<string>>(
                _roles.GetValueOrDefault(user.Id) ?? []);
        }

        public Task<bool> IsInRoleAsync(ApplicationUser user, string role)
        {
            return Task.FromResult(
                _roles.GetValueOrDefault(user.Id)?.Contains(role) == true);
        }

        public Task<IdentityResultSummary> CreateAsync(ApplicationUser user, string password)
        {
            _users.Add(user.Id, user);
            _usersByEmail.Add(user.Email!, user.Id);
            return Task.FromResult(IdentityResultSummary.Success);
        }

        public Task<IdentityResultSummary> UpdateAsync(ApplicationUser user) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> AddToRoleAsync(ApplicationUser user, string role)
        {
            if (!_roles.TryGetValue(user.Id, out List<string>? roles))
            {
                roles = [];
                _roles.Add(user.Id, roles);
            }

            roles.Add(role);
            return Task.FromResult(IdentityResultSummary.Success);
        }

        public Task<IdentityResultSummary> ConfirmEmailAsync(ApplicationUser user) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> SetPasswordAsync(
            ApplicationUser user,
            string newPassword) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> ChangePasswordAsync(
            ApplicationUser user,
            string currentPassword,
            string newPassword) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<ApplicationUser?> FindByLoginAsync(
            string loginProvider,
            string providerKey)
        {
            return Task.FromResult(
                _usersByLogin.TryGetValue(
                    LoginKey(loginProvider, providerKey),
                    out Guid userId)
                    ? _users[userId]
                    : null);
        }

        public Task<IdentityResultSummary> AddLoginAsync(
            ApplicationUser user,
            string loginProvider,
            string providerKey,
            string displayName)
        {
            _usersByLogin.Add(LoginKey(loginProvider, providerKey), user.Id);
            return Task.FromResult(IdentityResultSummary.Success);
        }

        public ApplicationUser AddExistingGoogleStudent(string providerKey, string email)
        {
            ApplicationUser user = new()
            {
                Email = email,
                UserName = email,
                FirstName = "Focus",
                LastName = "Student",
                EmailConfirmed = true
            };

            _users.Add(user.Id, user);
            _usersByEmail.Add(email, user.Id);
            _usersByLogin.Add(LoginKey("Google", providerKey), user.Id);
            _roles.Add(user.Id, [ApplicationRoles.Student]);

            return user;
        }

        private static string LoginKey(string loginProvider, string providerKey) => $"{loginProvider}:{providerKey}";
    }

    private sealed class FakeTokenProvider : ITokenProvider
    {
        public Task<TokenPair> CreateTokenPairAsync(
            ApplicationUser user,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TokenPair(
                "access-token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
        }

        public Task<(string Token, DateTimeOffset ExpiresOnUtc)> CreateOnboardingTokenAsync(
            ApplicationUser user) =>
            Task.FromResult(("registration-token", DateTimeOffset.UtcNow.AddMinutes(15)));

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string accessToken) => new();

        public string GenerateRefreshToken() => "refresh-token";

        public Task<RefreshToken> PersistRefreshTokenAsync(
            Guid userId,
            string refreshToken,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RefreshToken?> GetRefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RefreshToken?>(null);

        public Task<TokenPair?> RotateRefreshTokenAsync(
            ApplicationUser user,
            string refreshToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TokenPair?>(null);

        public Task<bool> RevokeRefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task RevokeAllRefreshTokensAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}