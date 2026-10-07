using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Students;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class ParentOverviewWeekHandlerTests
{
    [Fact]
    public async Task OverviewWeeks_ReturnsPersistedNoGoalNoSessionWeeks_AndCreatesCurrentWeek()
    {
        TestContext context = CreateContext();
        context.Weeks = new InMemoryRepository<StudentWeek>(new StudentWeek(context.Student.Id, new DateOnly(2026, 9, 26)));
        context.Handler = CreateHandler(context);

        var result = await context.Handler.Handle(new GetParentOverviewWeeksQuery(context.Student.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([new DateOnly(2026, 9, 26), new DateOnly(2026, 10, 3)], result.Value.Weeks.Select(x => x.StartsOn));
        Assert.False(result.Value.Weeks.Single(x => x.StartsOn == new DateOnly(2026, 9, 26)).IsCurrentWeek);
        Assert.True(result.Value.Weeks.Single(x => x.StartsOn == new DateOnly(2026, 10, 3)).IsCurrentWeek);
    }

    [Fact]
    public async Task SetOverviewWeek_AllowsExistingHistoricalWeekAfterWeekStartPreferenceChanges()
    {
        TestContext context = CreateContext();
        StudentWeek historical = new(context.Student.Id, new DateOnly(2026, 9, 26));
        context.Weeks = new InMemoryRepository<StudentWeek>(historical);
        context.Student.SetWeekStartsOn(DayOfWeek.Monday);
        context.Handler = CreateHandler(context);

        var result = await context.Handler.Handle(new SetParentOverviewWeekCommand(context.Student.Id, historical.StartsOn), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(historical.StartsOn, result.Value.StartsOn);
        Assert.Equal(historical.EndsOn, result.Value.EndsOn);
        Assert.False(result.Value.IsCurrentWeek);
    }

    [Fact]
    public async Task OverviewWeeks_KeepsCurrentWeekIndependentOfHistoricalSelections()
    {
        TestContext context = CreateContext();
        StudentWeek firstHistorical = new(context.Student.Id, new DateOnly(2026, 9, 21));
        StudentWeek secondHistorical = new(context.Student.Id, new DateOnly(2026, 9, 26));
        StudentWeek actualCurrent = new(context.Student.Id, new DateOnly(2026, 10, 3));
        context.Weeks = new InMemoryRepository<StudentWeek>(firstHistorical, secondHistorical, actualCurrent);
        context.Handler = CreateHandler(context);

        var firstSelection = await context.Handler.Handle(new SetParentOverviewWeekCommand(context.Student.Id, firstHistorical.StartsOn), CancellationToken.None);
        var afterFirstSelection = await context.Handler.Handle(new GetParentOverviewWeeksQuery(context.Student.Id), CancellationToken.None);
        var secondSelection = await context.Handler.Handle(new SetParentOverviewWeekCommand(context.Student.Id, secondHistorical.StartsOn), CancellationToken.None);
        var afterSecondSelection = await context.Handler.Handle(new GetParentOverviewWeeksQuery(context.Student.Id), CancellationToken.None);
        var currentSelection = await context.Handler.Handle(new SetParentOverviewWeekCommand(context.Student.Id, actualCurrent.StartsOn), CancellationToken.None);

        Assert.True(firstSelection.IsSuccess);
        Assert.False(firstSelection.Value.IsCurrentWeek);
        Assert.True(afterFirstSelection.IsSuccess);
        Assert.False(afterFirstSelection.Value.Weeks.Single(week => week.StartsOn == firstHistorical.StartsOn).IsCurrentWeek);
        Assert.True(afterFirstSelection.Value.Weeks.Single(week => week.StartsOn == actualCurrent.StartsOn).IsCurrentWeek);
        Assert.True(secondSelection.IsSuccess);
        Assert.False(secondSelection.Value.IsCurrentWeek);
        Assert.True(afterSecondSelection.IsSuccess);
        Assert.False(afterSecondSelection.Value.Weeks.Single(week => week.StartsOn == secondHistorical.StartsOn).IsCurrentWeek);
        Assert.True(afterSecondSelection.Value.Weeks.Single(week => week.StartsOn == actualCurrent.StartsOn).IsCurrentWeek);
        Assert.True(currentSelection.IsSuccess);
        Assert.True(currentSelection.Value.IsCurrentWeek);
    }

    [Fact]
    public async Task OverviewWeeks_UsesStudentsTimeZoneAndWeekStartToFindCurrentWeek()
    {
        TestContext context = CreateContext();
        context.Student.SetTimeZoneId("America/Los_Angeles");
        StudentWeek localCurrent = new(context.Student.Id, new DateOnly(2026, 9, 26));
        StudentWeek notYetCurrentInStudentTimeZone = new(context.Student.Id, new DateOnly(2026, 10, 3));
        context.Weeks = new InMemoryRepository<StudentWeek>(localCurrent, notYetCurrentInStudentTimeZone);
        context.Handler = CreateHandler(context, new DateTimeOffset(2026, 10, 2, 22, 30, 0, TimeSpan.Zero));

        var result = await context.Handler.Handle(new GetParentOverviewWeeksQuery(context.Student.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Weeks.Single(week => week.StartsOn == localCurrent.StartsOn).IsCurrentWeek);
        Assert.False(result.Value.Weeks.Single(week => week.StartsOn == notYetCurrentInStudentTimeZone.StartsOn).IsCurrentWeek);
    }

    [Fact]
    public async Task SetOverviewWeek_RejectsNonPersistedCalendarWeek()
    {
        TestContext context = CreateContext();

        var result = await context.Handler.Handle(new SetParentOverviewWeekCommand(context.Student.Id, new DateOnly(2026, 9, 26)), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("ParentOverviewWeek.NotFound", result.TopError.Code);
    }

    private static TestContext CreateContext()
    {
        Guid userId = Guid.NewGuid();
        Parent parent = new(userId);
        Student student = new(Guid.NewGuid());
        student.SetWeekStartsOn(DayOfWeek.Saturday);
        student.SetTimeZoneId("Africa/Cairo");
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        TestContext context = new(parent, student, relationship, new InMemoryRepository<StudentWeek>(), null!, userId);
        context.Handler = CreateHandler(context);
        return context;
    }

    private static ParentOverviewWeekHandler CreateHandler(TestContext context, DateTimeOffset? now = null) => new(
        new InMemoryRepository<Parent>(context.Parent),
        new InMemoryRepository<ParentStudentRelationship>(context.Relationship),
        new InMemoryRepository<Student>(context.Student),
        context.Weeks,
        new FakeCurrentUser(context.UserId),
        new FixedTimeProvider(now ?? new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero)));

    private sealed class TestContext(Parent parent, Student student, ParentStudentRelationship relationship, InMemoryRepository<StudentWeek> weeks, ParentOverviewWeekHandler handler, Guid userId)
    {
        public Parent Parent { get; } = parent;
        public Student Student { get; } = student;
        public ParentStudentRelationship Relationship { get; } = relationship;
        public InMemoryRepository<StudentWeek> Weeks { get; set; } = weeks;
        public ParentOverviewWeekHandler Handler { get; set; } = handler;
        public Guid UserId { get; } = userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
