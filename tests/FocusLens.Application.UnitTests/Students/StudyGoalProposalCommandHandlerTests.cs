using FocusLens.Application.Students;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;
using ContractStudyTimeGoalPeriod = FocusLens.Contracts.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.UnitTests.Students;

public sealed class StudyGoalProposalCommandHandlerTests
{
    [Fact]
    public async Task Create_WhenRelationshipIsActiveAndNoPendingProposal_CreatesWeeklyPendingProposalFromHours()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        parent.SetPrivateProperty("User", new ApplicationUser { FirstName = "Mona", LastName = "Hassan" });
        Student student = CreateStudent();
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        InMemoryRepository<StudyGoalProposal> proposalRepository = new();
        FakeUnitOfWork unitOfWork = new();

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            proposalRepository,
            new FakeCurrentUser(parentUserId),
            unitOfWork,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                student.Id,
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Weekly,
                    1.5m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudyGoalProposalStatus.Pending.ToString(), result.Value.Status);
        Assert.Equal(student.Id, result.Value.StudentId);
        Assert.Equal(parent.Id, result.Value.ParentId);
        Assert.Equal("Mona Hassan", result.Value.SuggestedByParentName);
        Assert.Equal(90, result.Value.Goal.TargetMinutes);
        Assert.Equal(new DateOnly(2026, 9, 14), result.Value.Goal.StartDate);
        Assert.Single(proposalRepository.GetAll());
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Create_WhenPeriodIsNotWeekly_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = CreateStudent();
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<StudyGoalProposal>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                student.Id,
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Daily,
                    1.5m)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Validation, result.TopError.Type);
        Assert.Equal("StudyGoalProposals.WeeklyPeriodRequired", result.TopError.Code);
    }

    [Fact]
    public async Task Create_ForWeeklyGoal_UsesParentsSelectedWeekStartDay()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId, DayOfWeek.Saturday);
        Student student = CreateStudent();
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        InMemoryRepository<StudyGoalProposal> proposalRepository = new();

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            proposalRepository,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                student.Id,
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Weekly,
                    3m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        StudyGoalProposal proposal = proposalRepository.GetAll().Single();
        Assert.Equal([DayOfWeek.Saturday], proposal.Goal.Days);
        Assert.Equal([DayOfWeek.Saturday], result.Value.Goal.Days);
        Assert.Equal(new DateOnly(2026, 9, 19), proposal.Goal.StartDate);
    }

    [Fact]
    public async Task Create_WhenParentHasNotSelectedWeekStart_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<StudyGoalProposal>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                Guid.NewGuid(),
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Weekly,
                    3m)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Validation, result.TopError.Type);
        Assert.Equal("StudyGoalProposals.WeekStartsOnRequired", result.TopError.Code);
    }

    [Fact]
    public async Task Create_WhenStudentAlreadyHasPendingProposal_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = CreateStudent();
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudyGoalProposal pendingProposal = new(
            parent.Id,
            student.Id,
            CreateGoal());

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<StudyGoalProposal>(pendingProposal),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                student.Id,
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Weekly,
                    3m)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Conflict, result.TopError.Type);
        Assert.Equal("StudyGoalProposals.PendingProposalExists", result.TopError.Code);
    }

    [Fact]
    public async Task Create_WhenStudentAlreadyAcceptedProposalInCurrentWeek_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = CreateStudent();
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudyGoalProposal acceptedProposal = new(
            parent.Id,
            student.Id,
            CreateGoal(new DateOnly(2026, 9, 14)));
        acceptedProposal.Accept(new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero));

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<StudyGoalProposal>(acceptedProposal),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                student.Id,
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Weekly,
                    3m)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Conflict, result.TopError.Type);
        Assert.Equal("StudyGoalProposals.AcceptedProposalExistsForCurrentWeek", result.TopError.Code);
    }

    [Fact]
    public async Task Create_WhenStudentOnlyRejectedProposalInCurrentWeek_CreatesReplacement()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = CreateStudent();
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudyGoalProposal rejectedProposal = new(
            parent.Id,
            student.Id,
            CreateGoal(new DateOnly(2026, 9, 14)));
        rejectedProposal.Reject(new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero));
        InMemoryRepository<StudyGoalProposal> proposalRepository = new(rejectedProposal);

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            proposalRepository,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                student.Id,
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Weekly,
                    3m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, proposalRepository.GetAll().Count());
        Assert.Contains(
            proposalRepository.GetAll(),
            proposal =>
                proposal.Id != rejectedProposal.Id &&
                proposal.Status == StudyGoalProposalStatus.Pending);
    }

    [Fact]
    public async Task Create_WhenReplacingRejectedProposal_StartsAtCalculatedWeekStart()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId, DayOfWeek.Saturday);
        Student student = CreateStudent();
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudyGoalProposal rejectedProposal = new(
            parent.Id,
            student.Id,
            CreateGoal(new DateOnly(2026, 9, 19), DayOfWeek.Saturday));
        rejectedProposal.Reject(new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero));
        InMemoryRepository<StudyGoalProposal> proposalRepository = new(rejectedProposal);

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            proposalRepository,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                student.Id,
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Weekly,
                    3m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 19), result.Value.Goal.StartDate);
        Assert.NotEqual(new DateOnly(2026, 9, 21), result.Value.Goal.StartDate);
    }

    [Fact]
    public async Task Create_WhenAcceptedProposalIsFromPreviousWeek_CreatesCurrentWeekProposal()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = CreateStudent();
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudyGoalProposal previousWeekAcceptedProposal = new(
            parent.Id,
            student.Id,
            CreateGoal(new DateOnly(2026, 9, 7)));
        previousWeekAcceptedProposal.Accept(new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        InMemoryRepository<StudyGoalProposal> proposalRepository = new(previousWeekAcceptedProposal);

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            proposalRepository,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                student.Id,
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Weekly,
                    3m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 14), result.Value.Goal.StartDate);
        Assert.Equal(2, proposalRepository.GetAll().Count());
    }

    [Fact]
    public async Task Create_WhenRelationshipIsMissing_ReturnsForbidden()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);

        CreateStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<StudyGoalProposal>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new CreateStudyGoalProposalCommand(
                Guid.NewGuid(),
                new CreateStudyGoalProposalRequest(
                    ContractStudyTimeGoalPeriod.Weekly,
                    3m)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Forbidden, result.TopError.Type);
    }

    [Fact]
    public async Task Accept_WhenProposalBelongsToStudent_AppliesGoalAndMarksAccepted()
    {
        Student student = CreateStudent();
        Parent parent = CreateParent(Guid.NewGuid());
        StudyGoalProposal proposal = new(parent.Id, student.Id, CreateGoal());
        proposal.SetPrivateProperty("Parent", parent);
        FakeUnitOfWork unitOfWork = new();

        AcceptStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(proposal),
            new FakeNotificationWriter(),
            new FakeCurrentUser(student.UserId),
            unitOfWork,
            TimeProvider.System);

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new AcceptStudyGoalProposalCommand(proposal.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudyGoalProposalStatus.Accepted, proposal.Status);
        Assert.NotNull(proposal.RespondedAtUtc);
        Assert.NotNull(student.StudyTimeGoal);
        Assert.Equal(DomainStudyTimeGoalPeriod.Weekly, student.StudyTimeGoal.Period);
        Assert.Equal(240, student.StudyTimeGoal.TargetMinutes);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Accept_WhenProposalIsNotPending_ReturnsConflictAndDoesNotChangeGoal()
    {
        Student student = CreateStudent();
        StudyGoalProposal proposal = new(Guid.NewGuid(), student.Id, CreateGoal());
        proposal.Reject(DateTimeOffset.UtcNow);

        AcceptStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(proposal),
            new FakeNotificationWriter(),
            new FakeCurrentUser(student.UserId),
            new FakeUnitOfWork(),
            TimeProvider.System);

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new AcceptStudyGoalProposalCommand(proposal.Id),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Conflict, result.TopError.Type);
        Assert.Null(student.StudyTimeGoal);
    }

    [Fact]
    public async Task Reject_WhenProposalBelongsToStudent_MarksRejectedWithoutChangingGoal()
    {
        Student student = CreateStudent();
        Parent parent = CreateParent(Guid.NewGuid());
        StudyGoalProposal proposal = new(parent.Id, student.Id, CreateGoal());
        proposal.SetPrivateProperty("Parent", parent);

        RejectStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(proposal),
            new FakeNotificationWriter(),
            new FakeCurrentUser(student.UserId),
            new FakeUnitOfWork(),
            TimeProvider.System);

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new RejectStudyGoalProposalCommand(proposal.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudyGoalProposalStatus.Rejected, proposal.Status);
        Assert.NotNull(proposal.RespondedAtUtc);
        Assert.Null(student.StudyTimeGoal);
    }

    [Fact]
    public async Task Reject_WhenProposalBelongsToDifferentStudent_ReturnsForbidden()
    {
        Student student = CreateStudent();
        StudyGoalProposal proposal = new(Guid.NewGuid(), Guid.NewGuid(), CreateGoal());

        RejectStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(proposal),
            new FakeNotificationWriter(),
            new FakeCurrentUser(student.UserId),
            new FakeUnitOfWork(),
            TimeProvider.System);

        Result<StudyGoalProposalResponse> result = await handler.Handle(
            new RejectStudyGoalProposalCommand(proposal.Id),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Forbidden, result.TopError.Type);
    }

    [Fact]
    public async Task Cancel_WhenProposalBelongsToParent_MarksCancelled()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        StudyGoalProposal proposal = new(parent.Id, Guid.NewGuid(), CreateGoal());

        CancelStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<StudyGoalProposal>(proposal),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            TimeProvider.System);

        Result<Success> result = await handler.Handle(
            new CancelStudyGoalProposalCommand(proposal.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudyGoalProposalStatus.Cancelled, proposal.Status);
        Assert.NotNull(proposal.RespondedAtUtc);
    }

    [Fact]
    public async Task Cancel_WhenProposalBelongsToDifferentParent_ReturnsForbidden()
    {
        Parent parent = new(Guid.NewGuid());
        StudyGoalProposal proposal = new(Guid.NewGuid(), Guid.NewGuid(), CreateGoal());

        CancelStudyGoalProposalCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<StudyGoalProposal>(proposal),
            new FakeCurrentUser(parent.UserId),
            new FakeUnitOfWork(),
            TimeProvider.System);

        Result<Success> result = await handler.Handle(
            new CancelStudyGoalProposalCommand(proposal.Id),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Forbidden, result.TopError.Type);
    }

    [Fact]
    public async Task GetMyStudyGoalProposals_ReturnsOnlyCurrentStudentsProposals()
    {
        Student student = CreateStudent();
        Parent parent = CreateParent(Guid.NewGuid());
        parent.SetPrivateProperty("User", new ApplicationUser { FirstName = "Ahmed", LastName = "Mahmoud" });
        StudyGoalProposal firstProposal = new(parent.Id, student.Id, CreateGoal());
        StudyGoalProposal otherProposal = new(Guid.NewGuid(), Guid.NewGuid(), CreateGoal());

        GetMyStudyGoalProposalsQueryHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<StudyGoalProposal>(firstProposal, otherProposal),
            new FakeCurrentUser(student.UserId));

        IReadOnlyList<StudyGoalProposalResponse> result = await handler.Handle(
            new GetMyStudyGoalProposalsQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(firstProposal.Id, result[0].Id);
        Assert.Equal("Ahmed Mahmoud", result[0].SuggestedByParentName);
    }

    private static StudyTimeGoal CreateGoal(
        DateOnly? startDate = null,
        DayOfWeek weekStartsOn = DayOfWeek.Monday)
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            DomainStudyTimeGoalPeriod.Weekly,
            240,
            [weekStartsOn],
            startDate ?? new DateOnly(2026, 9, 14));

        return result.Value;
    }

    private static Student CreateStudent()
    {
        Student student = new(Guid.NewGuid());
        student.SetPrivateProperty("User", new ApplicationUser
        {
            FirstName = "Test",
            LastName = "Student",
            Email = "test@example.com"
        });
        return student;
    }

    private static Parent CreateParent(Guid userId, DayOfWeek weekStartsOn = DayOfWeek.Monday)
    {
        Parent parent = new(userId);
        parent.SetWeekStartsOn(weekStartsOn);
        parent.SetPrivateProperty("User", new ApplicationUser
        {
            FirstName = "Test",
            LastName = "Parent",
            Email = "parent@example.com"
        });
        return parent;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
