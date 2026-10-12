using FocusLens.Application.BehavioralIntelligence;

namespace FocusLens.Application.UnitTests.BehavioralIntelligence;

public sealed class RecentSessionFocusTrendCalculatorTests
{
    [Fact]
    public void Calculate_RequiresCurrentScoreAndTwoEarlierScoredSessions()
    {
        Assert.Null(RecentSessionFocusTrendCalculator.Calculate(null, [80, 70]));
        Assert.Null(RecentSessionFocusTrendCalculator.Calculate(90, [80]));
        Assert.Null(RecentSessionFocusTrendCalculator.Calculate(90, [null, 80]));
    }

    [Theory]
    [InlineData(80, 70, 65, "IMPROVING")]
    [InlineData(55, 70, 65, "DECLINING")]
    [InlineData(68, 70, 65, "STABLE")]
    public void Calculate_ComparesCurrentScoreWithTwoRecentSubjectSessions(
        int currentScore, int newestPreviousScore, int olderPreviousScore, string expected)
    {
        Assert.Equal(expected, RecentSessionFocusTrendCalculator.Calculate(
            currentScore, [newestPreviousScore, olderPreviousScore]));
    }
}
