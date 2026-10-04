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

    private static ParentOverviewWeekHandler CreateHandler(TestContext context) => new(
        new InMemoryRepository<Parent>(context.Parent),
        new InMemoryRepository<ParentStudentRelationship>(context.Relationship),
        new InMemoryRepository<Student>(context.Student),
        context.Weeks,
        new FakeCurrentUser(context.UserId),
        new FixedTimeProvider(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero)));

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
