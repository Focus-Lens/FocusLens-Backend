using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.BehavioralIntelligence;

/// <summary>Session-level focus metrics derived from the ordered analysis windows.</summary>
public sealed record SessionFocusMetrics(int? FocusScore, string? FocusState, string? FocusTrend);

public static class SessionFocusMetricsCalculator
{
    private static readonly string[] StateSeverityOrder =
    [
        "DISTRACTION_DISENGAGEMENT",
        "WEAK_UNDERSTANDING",
        "SKIMMING",
        "CONTENT_DIFFICULTY",
        "NORMAL_FOCUSED"
    ];

    public static SessionFocusMetrics Calculate(IEnumerable<StudySessionBehaviorWindow> windows)
    {
        StudySessionBehaviorWindow[] ordered = windows
            .OrderBy(window => window.WindowIndex)
            .ThenBy(window => window.WindowStartUtc)
            .ToArray();

        // If any row predates active-time persistence, do not mix weighted and unweighted
        // windows for that session. Preserve its prior latest-window display; never pretend
        // wall-clock duration is measured active time.
        bool hasLegacyWeights = ordered.Any(window => !window.WindowActiveTimeSeconds.HasValue);

        int? focusScore = hasLegacyWeights
            ? ordered.LastOrDefault()?.FocusScore
            : CalculateWeightedFocusScore(ordered);

        string? focusState = hasLegacyWeights
            ? ordered.LastOrDefault()?.FocusState
            : CalculateDominantState(ordered);

        return new SessionFocusMetrics(focusScore, focusState, CalculateTrend(ordered));
    }

    private static int? CalculateWeightedFocusScore(IEnumerable<StudySessionBehaviorWindow> windows)
    {
        StudySessionBehaviorWindow[] weightedWindows = windows
            .Where(window => window.FocusScore.HasValue &&
                             window.WindowActiveTimeSeconds is double seconds &&
                             double.IsFinite(seconds) && seconds > 0)
            .ToArray();

        if (weightedWindows.Length == 0)
        {
            return null;
        }

        double totalWeight = weightedWindows.Sum(window => window.WindowActiveTimeSeconds!.Value);
        double weightedScore = weightedWindows.Sum(window =>
            window.FocusScore!.Value * window.WindowActiveTimeSeconds!.Value);

        return (int)Math.Round(weightedScore / totalWeight, MidpointRounding.AwayFromZero);
    }

    private static string? CalculateDominantState(IEnumerable<StudySessionBehaviorWindow> windows)
    {
        StudySessionBehaviorWindow[] allWindows = windows.ToArray();
        var stateWeights = allWindows
            .Where(window => !string.IsNullOrWhiteSpace(window.FocusState) &&
                             window.WindowActiveTimeSeconds is double seconds &&
                             double.IsFinite(seconds) && seconds > 0)
            .GroupBy(window => window.FocusState!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                State = group.OrderBy(window => window.WindowIndex).First().FocusState!.Trim(),
                Weight = Math.Round(
                    group.Sum(window => window.WindowActiveTimeSeconds!.Value),
                    6,
                    MidpointRounding.ToEven)
            })
            .ToArray();

        if (stateWeights.Length == 0)
        {
            // A scored window with a measured zero active-time weight still has an observed
            // state. When every scored window has zero weight, apply the documented severity
            // tie-break instead of losing all state information. Empty/unscored windows remain null.
            return allWindows
                .Where(window => window.FocusScore.HasValue &&
                                 !string.IsNullOrWhiteSpace(window.FocusState) &&
                                 window.WindowActiveTimeSeconds is double seconds &&
                                 double.IsFinite(seconds) && seconds == 0)
                .Select(window => window.FocusState!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetSeverity)
                .ThenBy(state => state, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        return stateWeights
            .OrderByDescending(item => item.Weight)
            .ThenBy(item => GetSeverity(item.State))
            .ThenBy(item => item.State, StringComparer.OrdinalIgnoreCase)
            .First()
            .State;
    }


    private static string? CalculateTrend(IEnumerable<StudySessionBehaviorWindow> windows)
    {
        int[] scores = windows
            .Where(window => window.FocusScore.HasValue)
            .OrderBy(window => window.WindowIndex)
            .ThenBy(window => window.WindowStartUtc)
            .Select(window => window.FocusScore!.Value)
            .TakeLast(3)
            .ToArray();

        if (scores.Length == 0)
        {
            return null;
        }

        if (scores.Length < 2)
        {
            return "STABLE";
        }

        int change = scores[^1] - scores[0];
        return change >= 8 ? "IMPROVING" : change <= -8 ? "DECLINING" : "STABLE";
    }

    private static int GetSeverity(string state)
    {
        int index = Array.FindIndex(
            StateSeverityOrder,
            candidate => string.Equals(candidate, state, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }
}
