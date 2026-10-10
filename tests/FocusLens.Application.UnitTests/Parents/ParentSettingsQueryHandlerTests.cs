using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class ParentSettingsQueryHandlerTests
{
    [Fact]
    public async Task Get_WhenWeekStartHasNotBeenSelected_ReturnsSelectionRequired()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        GetParentSettingsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(parentUserId));

        Result<ParentSettingsResponse> result = await handler.Handle(
            new GetParentSettingsQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.WeekStartsOn);
        Assert.True(result.Value.WeekStartSelectionRequired);
    }

    [Fact]
    public async Task Update_WithValidWeekStart_StoresExplicitSelection()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        FakeUnitOfWork unitOfWork = new();

        UpdateParentSettingsCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(parentUserId),
            unitOfWork);

        Result<ParentSettingsResponse> result = await handler.Handle(
            new UpdateParentSettingsCommand(
                new UpdateParentSettingsRequest(DayOfWeek.Saturday)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DayOfWeek.Saturday, parent.WeekStartsOn);
        Assert.Equal(DayOfWeek.Saturday, result.Value.WeekStartsOn);
        Assert.False(result.Value.WeekStartSelectionRequired);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Update_WithInvalidWeekStart_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        UpdateParentSettingsCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork());

        Result<ParentSettingsResponse> result = await handler.Handle(
            new UpdateParentSettingsCommand(
                new UpdateParentSettingsRequest((DayOfWeek)999)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Validation, result.TopError.Type);
        Assert.Equal("Parents.InvalidWeekStartsOn", result.TopError.Code);
        Assert.Null(parent.WeekStartsOn);
    }

    [Fact]
    public async Task Update_PreservesExistingGoalFieldsWhenParentChangesWeekStart()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        parent.SetWeekStartsOn(DayOfWeek.Monday);
        StudyTimeGoal existingGoal = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Weekly,
            240,
            [DayOfWeek.Monday],
            new DateOnly(2026, 9, 14)).Value;

        UpdateParentSettingsCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork());

        Result<ParentSettingsResponse> result = await handler.Handle(
            new UpdateParentSettingsCommand(
                new UpdateParentSettingsRequest(DayOfWeek.Saturday)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DayOfWeek.Saturday, parent.WeekStartsOn);
        Assert.Equal(StudyTimeGoalPeriod.Weekly, existingGoal.Period);
        Assert.Equal(240, existingGoal.TargetMinutes);
        Assert.Equal([DayOfWeek.Monday], existingGoal.Days);
        Assert.Equal(new DateOnly(2026, 9, 14), existingGoal.StartDate);
    }
}