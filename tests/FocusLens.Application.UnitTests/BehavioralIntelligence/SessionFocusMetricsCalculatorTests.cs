using FocusLens.Application.BehavioralIntelligence;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.UnitTests.BehavioralIntelligence;

public sealed class SessionFocusMetricsCalculatorTests
{
    [Fact]
    public void Calculate_UsesActiveTimeWeightedScoreAndDominantState_AndDerivesTrendFromLastThreeScores()
    {
        Guid sessionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        StudySessionBehaviorWindow[] windows =
        [
            CreateWindow(sessionId, 1, start, 40, "SKIMMING", 60),
            CreateWindow(sessionId, 2, start.AddMinutes(5), 80, "NORMAL_FOCUSED", 180)
        ];

        SessionFocusMetrics result = SessionFocusMetricsCalculator.Calculate(windows);

        Assert.Equal(70, result.FocusScore);
        Assert.Equal("NORMAL_FOCUSED", result.FocusState);
        Assert.Equal("IMPROVING", result.FocusTrend);
    }

    [Fact]
    public void Calculate_UsesSeverityOrderToBreakEqualStateWeights()
    {
        Guid sessionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        StudySessionBehaviorWindow[] windows =
        [
            CreateWindow(sessionId, 1, start, 50, "CONTENT_DIFFICULTY", 60),
            CreateWindow(sessionId, 2, start.AddMinutes(5), 55, "SKIMMING", 60)
        ];

        SessionFocusMetrics result = SessionFocusMetricsCalculator.Calculate(windows);

        Assert.Equal("SKIMMING", result.FocusState);
    }

    [Fact]
    public void Calculate_UsesSeverityOrderWhenEveryScoredWindowHasZeroActiveTime()
    {
        Guid sessionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        StudySessionBehaviorWindow[] windows =
        [
            CreateWindow(sessionId, 1, start, 70, "CONTENT_DIFFICULTY", 0),
            CreateWindow(sessionId, 2, start.AddMinutes(5), 65, "SKIMMING", 0)
        ];

        SessionFocusMetrics result = SessionFocusMetricsCalculator.Calculate(windows);

        Assert.Null(result.FocusScore);
        Assert.Equal("SKIMMING", result.FocusState);
        Assert.Equal("STABLE", result.FocusTrend);
    }

    [Theory]
    [InlineData(new[] { 90, 30, 35, 48 }, "IMPROVING")]
    [InlineData(new[] { 90, 75, 70, 50 }, "DECLINING")]
    [InlineData(new[] { 10, 40, 44, 45 }, "STABLE")]
    public void Calculate_UsesOnlyTheLastThreeNonNullScoresForTrend(int[] scores, string expectedTrend)
    {
        Guid sessionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        StudySessionBehaviorWindow[] windows = scores
            .Select((score, index) => CreateWindow(
                sessionId, index + 1, start.AddMinutes(index * 5), score, "NORMAL_FOCUSED", 60))
            .ToArray();

        Assert.Equal(expectedTrend, SessionFocusMetricsCalculator.Calculate(windows).FocusTrend);
    }

    [Fact]
    public void Calculate_PreservesLatestMetricsForRowsWithoutPersistedActiveTime()
    {
        Guid sessionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        StudySessionBehaviorWindow[] windows =
        [
            CreateWindow(sessionId, 1, start, 45, "SKIMMING", null),
            CreateWindow(sessionId, 2, start.AddMinutes(5), 68, "NORMAL_FOCUSED", null)
        ];

        SessionFocusMetrics result = SessionFocusMetricsCalculator.Calculate(windows);

        Assert.Equal(68, result.FocusScore);
        Assert.Equal("NORMAL_FOCUSED", result.FocusState);
        Assert.Equal("IMPROVING", result.FocusTrend);
    }

    [Fact]
    public void Calculate_ReturnsNullMetricsForEmptyOrUnscoredNewWindows()
    {
        Assert.Equal(new SessionFocusMetrics(null, null, null),
            SessionFocusMetricsCalculator.Calculate([]));

        StudySessionBehaviorWindow emptyWindow = StudySessionBehaviorWindow.Create(
            Guid.NewGuid(), 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false).Value;
        Assert.True(emptyWindow.RecordAnalysis(null, "NORMAL_FOCUSED", "STABLE",
            null, null, "CONTINUE", "CONTINUE", false, 0).IsSuccess);

        Assert.Equal(new SessionFocusMetrics(null, null, null),
            SessionFocusMetricsCalculator.Calculate([emptyWindow]));
    }

    private static StudySessionBehaviorWindow CreateWindow(
        Guid sessionId,
        int index,
        DateTimeOffset start,
        int? score,
        string state,
        double? activeSeconds)
    {
        StudySessionBehaviorWindow window = StudySessionBehaviorWindow.Create(
            sessionId, index, start, start.AddMinutes(5), false).Value;
        Assert.True(window.RecordAnalysis(score, state, "STABLE", null, null,
            "CONTINUE", "CONTINUE", false, activeSeconds).IsSuccess);
        return window;
    }
}