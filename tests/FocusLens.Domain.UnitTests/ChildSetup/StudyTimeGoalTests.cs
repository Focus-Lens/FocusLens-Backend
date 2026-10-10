using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.UnitTests.ChildSetup;

public class StudyTimeGoalTests
{
    [Fact]
    public void Create_WithPositiveDailyTargetAndDays_CreatesGoal()
    {
        DateOnly startDate = new(2026, 9, 14);

        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Daily,
            60,
            [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            startDate);

        Assert.True(result.IsSuccess);

        StudyTimeGoal goal = result.Value;

        Assert.Equal(StudyTimeGoalPeriod.Daily, goal.Period);
        Assert.Equal(60, goal.TargetMinutes);
        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            goal.Days);
        Assert.Equal(startDate, goal.StartDate);
    }

    [Fact]
    public void Create_WithZeroTarget_ReturnsError()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Daily,
            0,
            [DayOfWeek.Monday],
            null);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithNegativeTarget_ReturnsError()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Weekly,
            -10,
            [],
            null);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithInvalidPeriod_ReturnsError()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            (StudyTimeGoalPeriod)999,
            60,
            [],
            null);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithDailyGoalAndNoDays_ReturnsError()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Daily,
            60,
            [],
            new DateOnly(2026, 9, 14));

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithWeeklyGoalAndMultipleDays_ReturnsError()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Weekly,
            60,
            [DayOfWeek.Monday, DayOfWeek.Tuesday],
            new DateOnly(2026, 9, 14));

        Assert.True(result.IsError);
        Assert.Equal("StudyTimeGoals.WeeklyStartDayRequired", result.TopError.Code);
    }

    [Fact]
    public void Create_WithDuplicateDays_ReturnsError()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Daily,
            60,
            [DayOfWeek.Monday, DayOfWeek.Monday],
            new DateOnly(2026, 9, 14));

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithInvalidDay_ReturnsError()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Daily,
            60,
            [(DayOfWeek)999],
            new DateOnly(2026, 9, 14));

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithMissingStartDate_ReturnsError()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Daily,
            60,
            [DayOfWeek.Monday],
            null);

        Assert.True(result.IsError);
        Assert.Equal("StudyTimeGoals.StartDateRequired", result.TopError.Code);
    }

    [Fact]
    public void Create_WithWeeklyGoalAndNoDays_ReturnsError()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Weekly,
            480,
            [],
            new DateOnly(2026, 9, 14));

        Assert.True(result.IsError);
        Assert.Equal("StudyTimeGoals.WeeklyStartDayRequired", result.TopError.Code);
    }

    [Fact]
    public void Create_WithWeeklyGoalAndOneStartDay_CreatesGoal()
    {
        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Weekly,
            480,
            [DayOfWeek.Saturday],
            new DateOnly(2026, 9, 12));

        Assert.True(result.IsSuccess);

        StudyTimeGoal goal = result.Value;

        Assert.Equal(StudyTimeGoalPeriod.Weekly, goal.Period);
        Assert.Equal(480, goal.TargetMinutes);
        Assert.Equal([DayOfWeek.Saturday], goal.Days);
        Assert.Equal(new DateOnly(2026, 9, 12), goal.StartDate);
    }
}