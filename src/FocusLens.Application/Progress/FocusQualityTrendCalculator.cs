using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.Progress;

/// <summary>Shared Behavioral Progress focus trend semantics.</summary>
public static class FocusQualityTrendCalculator
{
    public const int MinimumAnalyzedDays = 3;

    public static string? Calculate(IEnumerable<StudySessionBehaviorWindow> windows, DateOnly from, DateOnly to,
        Func<StudySessionBehaviorWindow, DateOnly> getDate)
    {
        int[] scores = windows.Where(window => getDate(window) >= from && getDate(window) <= to)
            .GroupBy(getDate).OrderBy(group => group.Key)
            .Select(group => group.Where(window => window.FocusScore.HasValue)
                .Select(window => window.FocusScore!.Value).ToArray())
            .Where(scores => scores.Length > 0)
            .Select(scores => (int)Math.Round(scores.Average(), MidpointRounding.AwayFromZero)).ToArray();
        if (scores.Length < MinimumAnalyzedDays)
        {
            return null;
        }

        int change = scores[^1] - scores[0];
        return change >= 5 ? "Improving" : change <= -5 ? "Declining" : "Steady";
    }
}