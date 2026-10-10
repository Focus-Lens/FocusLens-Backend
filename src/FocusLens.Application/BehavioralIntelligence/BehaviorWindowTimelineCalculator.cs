namespace FocusLens.Application.BehavioralIntelligence;

public sealed record BehaviorPauseInterval(DateTimeOffset StartedAtUtc, DateTimeOffset EndedAtUtc);

public sealed class BehaviorWindowTimelineCalculator
{
    public const int WindowActiveSeconds = 300;

    public IReadOnlyCollection<BehaviorWindowTimelineItem> Calculate(
        DateTimeOffset start,
        IReadOnlyCollection<BehaviorPauseInterval> pauses,
        DateTimeOffset end,
        bool final
    )
    {
        if (end < start)
        {
            throw new ArgumentException("Timeline end precedes session start.");
        }

        BehaviorPauseInterval[] p = pauses.OrderBy(x => x.StartedAtUtc).ToArray();
        DateTimeOffset prev = start;
        foreach (BehaviorPauseInterval x in p)
        {
            if (
                x.StartedAtUtc < start
                || x.EndedAtUtc <= x.StartedAtUtc
                || x.EndedAtUtc > end
                || x.StartedAtUtc < prev
            )
            {
                throw new ArgumentException("Pause intervals are malformed.");
            }

            prev = x.EndedAtUtc;
        }

        List<BehaviorWindowTimelineItem> result = new();
        DateTimeOffset cursor = start,
            windowStart = start;
        int used = 0,
            index = 1;
        foreach (BehaviorPauseInterval? pause in p.Append(null))
        {
            DateTimeOffset activeEnd = pause?.StartedAtUtc ?? end;
            while (cursor < activeEnd)
            {
                int need = WindowActiveSeconds - used;
                TimeSpan available = activeEnd - cursor;
                if (available.TotalSeconds >= need)
                {
                    DateTimeOffset boundary = cursor.AddSeconds(need);
                    result.Add(new BehaviorWindowTimelineItem(index++, windowStart, boundary, false));
                    windowStart = boundary;
                    used = 0;
                    cursor = boundary;
                }
                else
                {
                    used += (int)available.TotalSeconds;
                    cursor = activeEnd;
                }
            }

            if (pause is not null)
            {
                cursor = pause.EndedAtUtc;
                if (used == 0)
                {
                    windowStart = cursor;
                }
            }
        }

        if (final && used > 0)
        {
            result.Add(new BehaviorWindowTimelineItem(index, windowStart, end, true));
        }

        return result;
    }
}

public sealed record BehaviorWindowTimelineItem(
    int WindowIndex,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    bool IsFinal
);