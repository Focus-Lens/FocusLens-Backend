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
using StudentGoal = FocusLens.Domain.Students.StudentGoal;

namespace FocusLens.Application.UnitTests.Students;

public class CompleteStudentOnboardingCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenAllPreferencesAreSkipped_ReturnsIncompleteOnboardingResponse()
    {
        Student student = new(Guid.NewGuid());

        CompleteStudentOnboardingResponse result = (await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest("  كريم  ", null, null, null, null, null)),
            CancellationToken.None)).Value;

        Assert.False(student.IsOnboardingCompleted);
        Assert.Equal(student.UserId, result.UserId);
        Assert.Equal("Focus", result.FirstName);
        Assert.Equal("Student", result.LastName);
        Assert.Equal("incomplete", result.OnboardingStatus);
    }

    [Fact]
    public async Task Handle_WhenDateOfBirthIsMissing_ReturnsIncompleteOnboardingResponse()
    {
        Student student = new(Guid.NewGuid());

        CompleteStudentOnboardingResponse result = (await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    "  كريم  ",
                    null,
                    ContractStudentGoal.FocusBetter,
                    ContractStudentGrade.Grade10,
                    [new StudentSubjectRequest(ContractStudentSubjectType.Math, null)],
                    null)),
            CancellationToken.None)).Value;

        Assert.False(student.IsOnboardingCompleted);
        Assert.Equal(student.UserId, result.UserId);
        Assert.Equal("Focus", result.FirstName);
        Assert.Equal("Student", result.LastName);
        Assert.Equal("incomplete", result.OnboardingStatus);
    }

    [Fact]
    public async Task Handle_WhenAllPreferencesAreProvided_ReturnsCompletedOnboardingResponse()
    {
        Student student = new(Guid.NewGuid());

        CompleteStudentOnboardingResponse result = (await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    "  كريم  ",
                    new DateOnly(2010, 5, 12),
                    ContractStudentGoal.FocusBetter,
                    ContractStudentGrade.Grade10,
                    [new StudentSubjectRequest(ContractStudentSubjectType.Math, null)],
                    null)),
            CancellationToken.None)).Value;

        Assert.True(student.IsOnboardingCompleted);
        Assert.Equal("كريم", student.PreferredName);
        Assert.Equal(new DateOnly(2010, 5, 12), student.DateOfBirth);
        Assert.Equal(
            Domain.Students.StudentGoal.FocusBetter,
            student.Goal);
        Assert.Null(student.StudyTimeGoal);
        Assert.Equal(student.UserId, result.UserId);
        Assert.Equal("Focus", result.FirstName);
        Assert.Equal("Student", result.LastName);
        Assert.Equal("completed", result.OnboardingStatus);
    }

    private static CompleteStudentOnboardingCommandHandler CreateHandler(Student student)
    {
        return new CompleteStudentOnboardingCommandHandler(
            new InMemoryRepository<Student>(student),
            new FakeCurrentUser(student.UserId),
            new FakeUnitOfWork(),
            new TestIdentityService(student.UserId));
    }

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

        public Task<ApplicationUser?> FindByIdAsync(Guid id) =>
            Task.FromResult<ApplicationUser?>(id == _user.Id ? _user : null);

        public Task<ApplicationUser?> FindByEmailAsync(string email) => Task.FromResult<ApplicationUser?>(null);

        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password) => Task.FromResult(true);

        public Task<bool> IsEmailConfirmedAsync(ApplicationUser user) => Task.FromResult(true);

        public Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(false);

        public Task<IReadOnlyCollection<string>> GetRolesAsync(ApplicationUser user) =>
            Task.FromResult<IReadOnlyCollection<string>>([ApplicationRoles.Student]);

        public Task<bool> IsInRoleAsync(ApplicationUser user, string role) =>
            Task.FromResult(role == ApplicationRoles.Student);

        public Task<IdentityResultSummary> CreateAsync(ApplicationUser user, string password) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> UpdateAsync(ApplicationUser user) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> AddToRoleAsync(ApplicationUser user, string role) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> ConfirmEmailAsync(ApplicationUser user) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> SetPasswordAsync(ApplicationUser user, string newPassword) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> ChangePasswordAsync(ApplicationUser user, string currentPassword,
            string newPassword) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey) =>
            Task.FromResult<ApplicationUser?>(null);

        public Task<IdentityResultSummary> AddLoginAsync(ApplicationUser user, string loginProvider,
            string providerKey, string displayName) =>
            Task.FromResult(IdentityResultSummary.Success);
    }

}
