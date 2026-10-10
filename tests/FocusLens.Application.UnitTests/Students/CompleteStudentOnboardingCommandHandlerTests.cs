using FocusLens.Application.Students;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using ContractStudentGoal = FocusLens.Contracts.Students.StudentGoal;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using StudentGoal = FocusLens.Domain.Students.StudentGoal;
using StudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.UnitTests.Students;

public class CompleteStudentOnboardingCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenAllPreferencesAreSkipped_ReturnsIncompleteOnboardingResponse()
    {
        Student student = new(Guid.NewGuid());

        CompleteStudentOnboardingResponse result = (await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest("  كريم  ", null, null, null, null)),
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
                    null,
                    new OnboardingStudyTimeGoalRequest(1)
                )),
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
                    null,
                    new OnboardingStudyTimeGoalRequest(1)
                )),
            CancellationToken.None)).Value;

        Assert.True(student.IsOnboardingCompleted);
        Assert.Equal("كريم", student.PreferredName);
        Assert.Equal(new DateOnly(2010, 5, 12), student.DateOfBirth);
        Assert.Equal(
            StudentGoal.FocusBetter,
            student.Goal);
        Assert.NotNull(student.StudyTimeGoal);
        Assert.Equal(student.UserId, result.UserId);
        Assert.Equal("Focus", result.FirstName);
        Assert.Equal("Student", result.LastName);
        Assert.Equal("completed", result.OnboardingStatus);
    }

    [Fact]
    public async Task Handle_WhenOnboardingStudyTimeGoalIsProvided_CreatesWeeklyGoalFromSaturdayByDefault()
    {
        Student student = new(Guid.NewGuid());

        Result<CompleteStudentOnboardingResponse> result = await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    "Student",
                    null,
                    null,
                    null,
                    null,
                    StudyTimeGoal: new OnboardingStudyTimeGoalRequest(5))),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(student.StudyTimeGoal);
        Assert.Equal(StudyTimeGoalPeriod.Weekly, student.StudyTimeGoal!.Period);
        Assert.Equal(300, student.StudyTimeGoal.TargetMinutes);
        Assert.Equal([DayOfWeek.Saturday], student.StudyTimeGoal.Days);
        Assert.Equal(new DateOnly(2026, 9, 12), student.StudyTimeGoal.StartDate);
    }

    [Fact]
    public async Task Handle_WhenStudentHasWeekStart_UsesStudentWeekStart()
    {
        Student student = new(Guid.NewGuid());
        student.SetWeekStartsOn(DayOfWeek.Monday);

        Result<CompleteStudentOnboardingResponse> result = await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    "Student", null, null, null, null,
                    StudyTimeGoal: new OnboardingStudyTimeGoalRequest(2.5m))),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal([DayOfWeek.Monday], student.StudyTimeGoal!.Days);
        Assert.Equal(new DateOnly(2026, 9, 14), student.StudyTimeGoal.StartDate);
        Assert.Equal(150, student.StudyTimeGoal.TargetMinutes);
    }

    [Fact]
    public async Task Handle_WhenActiveParentHasWeekStart_UsesParentWeekStartBeforeStudentSetting()
    {
        Student student = new(Guid.NewGuid());
        student.SetWeekStartsOn(DayOfWeek.Monday);
        Parent parent = new(Guid.NewGuid());
        parent.SetWeekStartsOn(DayOfWeek.Sunday);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        relationship.SetPrivateProperty("Parent", parent);

        Result<CompleteStudentOnboardingResponse> result = await CreateHandler(student, relationship).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    "Student", null, null, null, null,
                    StudyTimeGoal: new OnboardingStudyTimeGoalRequest(2))),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal([DayOfWeek.Sunday], student.StudyTimeGoal!.Days);
        Assert.Equal(new DateOnly(2026, 9, 13), student.StudyTimeGoal.StartDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999999999)]
    public async Task Handle_WhenTargetHoursIsInvalid_ReturnsValidationError(decimal targetHours)
    {
        Student student = new(Guid.NewGuid());

        Result<CompleteStudentOnboardingResponse> result = await CreateHandler(student).Handle(
            new CompleteStudentOnboardingCommand(
                new CompleteStudentOnboardingRequest(
                    "Student", null, null, null, null,
                    StudyTimeGoal: new OnboardingStudyTimeGoalRequest(targetHours))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Students.InvalidOnboardingStudyTimeGoal", result.TopError.Code);
        Assert.Null(student.StudyTimeGoal);
    }

    private static CompleteStudentOnboardingCommandHandler CreateHandler(
        Student student,
        ParentStudentRelationship? relationship = null)
    {
        return new CompleteStudentOnboardingCommandHandler(
            new InMemoryRepository<Student>(student),
            relationship is null
                ? new InMemoryRepository<ParentStudentRelationship>()
                : new InMemoryRepository<ParentStudentRelationship>(relationship),
            new FakeCurrentUser(student.UserId),
            new FakeUnitOfWork(),
            new TestIdentityService(student.UserId),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
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