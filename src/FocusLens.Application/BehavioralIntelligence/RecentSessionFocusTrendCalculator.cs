namespace FocusLens.Application.BehavioralIntelligence;

/// <summary>Compares a session's score with the two most recent scored sessions of the same subject.</summary>
public static class RecentSessionFocusTrendCalculator
{
    private const int MinimumScoreChangeForTrend = 8;
    private const int RequiredPreviousSessions = 2;

    public static string? Calculate(int? currentFocusScore, IEnumerable<int?> previousFocusScores)
    {
        ArgumentNullException.ThrowIfNull(previousFocusScores);

        if (currentFocusScore is null)
        {
            return null;
        }

        int[] previousScores = previousFocusScores
            .Where(score => score.HasValue)
            .Select(score => score!.Value)
            .Take(RequiredPreviousSessions)
            .ToArray();

        if (previousScores.Length < RequiredPreviousSessions)
        {
            return null;
        }

        int previousAverage = (int)Math.Round(previousScores.Average(), MidpointRounding.AwayFromZero);
        int change = currentFocusScore.Value - previousAverage;
        return change >= MinimumScoreChangeForTrend ? "IMPROVING"
            : change <= -MinimumScoreChangeForTrend ? "DECLINING"
            : "STABLE";
    }
}
