using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using DomainStudentGrade = FocusLens.Domain.Students.StudentGrade;
using StudyTimeGoal = FocusLens.Domain.Students.StudyTimeGoal;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class ActivateChildSetupCommandHandlerTests
{
    [Fact]
    public async Task Activate_WithParentWeekStart_PersistsWeekStartOnStudent()
    {
        Guid studentUserId = Guid.NewGuid();
        Parent parent = new(Guid.NewGuid());
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        Student student = new(studentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);
        ApplicationUser user = new()
        {
            Id = studentUserId,
            Email = "student@example.com",
            UserName = "student@example.com",
            FirstName = "Student",
            LastName = "Name"
        };
        student.SetPrivateProperty("User", user);

        ActivateChildSetupCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(studentUserId),
            new TestIdentityService(user),
            new FakeUnitOfWork(),
            new InMemoryRepository<Parent>(parent));

        Result<StudentDetailsResponse> result = await handler.Handle(
            new ActivateChildSetupCommand(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DayOfWeek.Saturday, student.WeekStartsOn);
    }

    [Fact]
    public async Task Activate_WithEmptyClaimedDraft_PreservesStudentOnboardingProfile()
    {
        Guid studentUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();
        Student student = new(studentUserId);
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);

        ApplicationUser user = new()
        {
            Id = studentUserId,
            Email = "youssef@example.com",
            UserName = "youssef@example.com",
            FirstName = "Youssef",
            LastName = "Student",
        };

        student.SetPrivateProperty("User", user);

        ActivateChildSetupCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(studentUserId),
            new TestIdentityService(user),
            new FakeUnitOfWork()
        );

        Result<StudentDetailsResponse> result = await handler.Handle(
            new ActivateChildSetupCommand(),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(ChildSetupStatus.Activated, draft.Status);
        Assert.Equal("Youssef", user.FirstName);
        Assert.Equal("Student", user.LastName);
    }

    [Fact]
    public async Task Activate_WithClaimedDraftWithProfileImage_TransfersImageToStudent()
    {
        Guid studentUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();

        Student student = new(studentUserId);
        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);
        draft.SetProfileImageStorageReference("images/draft-profile.jpg");

        ApplicationUser user = new()
        {
            Id = studentUserId,
            Email = "youssef@example.com",
            UserName = "youssef@example.com",
            FirstName = "Youssef",
            LastName = "Student",
        };

        student.SetPrivateProperty("User", user);

        ActivateChildSetupCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(studentUserId),
            new TestIdentityService(user),
            new FakeUnitOfWork()
        );

        Result<StudentDetailsResponse> result = await handler.Handle(
            new ActivateChildSetupCommand(),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(ChildSetupStatus.Activated, draft.Status);
        Assert.Equal(
            "images/draft-profile.jpg",
            student.ProfileImageStorageReference
        );
    }

    [Fact]
    public async Task Activate_WithClaimedDraft_CreatesActiveParentStudentRelationship()
    {
        Guid studentUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();

        Student student = new(studentUserId);
        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(DomainStudentGrade.Grade10);
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);

        InMemoryRepository<ParentStudentRelationship> relationships = new();

        ApplicationUser user = new()
        {
            Id = studentUserId,
            Email = "youssef@example.com",
            UserName = "youssef@example.com",
            FirstName = "Old",
            LastName = "Name",
        };

        student.SetPrivateProperty("User", user);

        ActivateChildSetupCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            relationships,
            new FakeCurrentUser(studentUserId),
            new TestIdentityService(user),
            new FakeUnitOfWork()
        );

        Result<StudentDetailsResponse> result = await handler.Handle(
            new ActivateChildSetupCommand(),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(ChildSetupStatus.Activated, draft.Status);

        ParentStudentRelationship relationship = Assert.Single(await relationships.GetAllAsync());

        Assert.Equal(parent.Id, relationship.ParentId);
        Assert.Equal(student.Id, relationship.StudentId);
        Assert.Equal(RelationshipStatus.Active, relationship.Status);
        Assert.True(student.ShareSessionSummariesWithParents);
        Assert.True(student.ShareSubjectTrendsWithParents);
        Assert.True(student.ShareDetailedAnswersWithParents);
    }

    [Fact]
    public async Task Activate_WithExistingActiveRelationship_DoesNotCreateDuplicate()
    {
        Guid studentUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();

        Student student = new(studentUserId);
        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(DomainStudentGrade.Grade10);
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);

        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();

        InMemoryRepository<ParentStudentRelationship> relationships = new(relationship);

        ApplicationUser user = new()
        {
            Id = studentUserId,
            Email = "youssef@example.com",
            UserName = "youssef@example.com",
            FirstName = "Old",
            LastName = "Name",
        };

        student.SetPrivateProperty("User", user);

        ActivateChildSetupCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            relationships,
            new FakeCurrentUser(studentUserId),
            new TestIdentityService(user),
            new FakeUnitOfWork()
        );

        Result<StudentDetailsResponse> result = await handler.Handle(
            new ActivateChildSetupCommand(),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(RelationshipStatus.Active, relationship.Status);
        Assert.Equal(ChildSetupStatus.Activated, draft.Status);
        Assert.Single(await relationships.GetAllAsync());
    }

    [Fact]
    public async Task Activate_WithRevokedRelationship_ReactivatesRelationship()
    {
        Guid studentUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();

        Student student = new(studentUserId);
        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(DomainStudentGrade.Grade10);
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);

        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        relationship.Revoke();

        InMemoryRepository<ParentStudentRelationship> relationships = new(relationship);

        ApplicationUser user = new()
        {
            Id = studentUserId,
            Email = "youssef@example.com",
            UserName = "youssef@example.com",
            FirstName = "Old",
            LastName = "Name",
        };

        student.SetPrivateProperty("User", user);

        ActivateChildSetupCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            relationships,
            new FakeCurrentUser(studentUserId),
            new TestIdentityService(user),
            new FakeUnitOfWork()
        );

        Result<StudentDetailsResponse> result = await handler.Handle(
            new ActivateChildSetupCommand(),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(RelationshipStatus.Active, relationship.Status);
        Assert.Equal(ChildSetupStatus.Activated, draft.Status);
        Assert.Single(await relationships.GetAllAsync());
    }

    [Fact]
    public async Task Activate_WithAlreadyActivatedDraft_ReturnsNotReadyForActivation()
    {
        Guid studentUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();

        Student student = new(studentUserId);
        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.SetGrade(DomainStudentGrade.Grade10);
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);
        draft.MarkActivated();

        ActivateChildSetupCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(studentUserId),
            new TestIdentityService(
                new ApplicationUser
                {
                    Id = studentUserId,
                    Email = "youssef@example.com",
                    UserName = "youssef@example.com",
                    FirstName = "Youssef",
                    LastName = "Mahmoud",
                }
            ),
            new FakeUnitOfWork()
        );

        Result<StudentDetailsResponse> result = await handler.Handle(
            new ActivateChildSetupCommand(),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.NotReadyForActivation", result.TopError.Code);
    }

    private sealed class TestIdentityService(ApplicationUser user) : IIdentityService
    {
        public Task<ApplicationUser?> FindByIdAsync(Guid userId) =>
            Task.FromResult<ApplicationUser?>(userId == user.Id ? user : null);

        public Task<ApplicationUser?> FindByEmailAsync(string email) =>
            Task.FromResult<ApplicationUser?>(null);

        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password) =>
            Task.FromResult(true);

        public Task<bool> IsEmailConfirmedAsync(ApplicationUser user) => Task.FromResult(true);

        public Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(false);

        public Task<IReadOnlyCollection<string>> GetRolesAsync(ApplicationUser user) =>
            Task.FromResult<IReadOnlyCollection<string>>([ApplicationRoles.Student]);

        public Task<bool> IsInRoleAsync(ApplicationUser user, string role) => Task.FromResult(true);

        public Task<IdentityResultSummary> CreateAsync(ApplicationUser user, string password) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> UpdateAsync(ApplicationUser user) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> AddToRoleAsync(ApplicationUser user, string role) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> ConfirmEmailAsync(ApplicationUser user) =>
            Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> SetPasswordAsync(
            ApplicationUser user,
            string newPassword
        ) => Task.FromResult(IdentityResultSummary.Success);

        public Task<IdentityResultSummary> ChangePasswordAsync(
            ApplicationUser user,
            string currentPassword,
            string newPassword
        ) => Task.FromResult(IdentityResultSummary.Success);

        public Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey) =>
            Task.FromResult<ApplicationUser?>(null);

        public Task<IdentityResultSummary> AddLoginAsync(
            ApplicationUser user,
            string loginProvider,
            string providerKey,
            string displayName
        ) => Task.FromResult(IdentityResultSummary.Success);
    }
    
    [Fact]
    public async Task Activate_WithClaimedDraftWithStudyTimeGoal_TransfersGoalToStudent()
    {
        Guid studentUserId = Guid.NewGuid();
        Guid parentUserId = Guid.NewGuid();

        Student student = new(studentUserId);
        Parent parent = new(parentUserId);

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Youssef", "Mahmoud");
        draft.MarkInvited();
        draft.MarkClaimed(student.Id);
        draft.SetStudyTimeGoal(
            StudyTimeGoal
                .Create(
                    DomainStudyTimeGoalPeriod.Daily,
                    60,
                    [DayOfWeek.Monday],
                    new DateOnly(2026, 9, 21))
                .Value);

        ApplicationUser user = new()
        {
            Id = studentUserId,
            Email = "youssef@example.com",
            UserName = "youssef@example.com",
            FirstName = "Old",
            LastName = "Name",
        };

        student.SetPrivateProperty("User", user);

        ActivateChildSetupCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ParentStudentRelationship>(),
            new FakeCurrentUser(studentUserId),
            new TestIdentityService(user),
            new FakeUnitOfWork());

        Result<StudentDetailsResponse> result = await handler.Handle(
            new ActivateChildSetupCommand(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(student.StudyTimeGoal);
        Assert.Equal(60, student.StudyTimeGoal.TargetMinutes);
        Assert.Equal(
            Domain.Students.StudyTimeGoalPeriod.Daily,
            student.StudyTimeGoal.Period);
    }

}
