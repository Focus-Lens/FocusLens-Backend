namespace FocusLens.Contracts;

public sealed record ParentSettingsResponse(
    DayOfWeek? WeekStartsOn,
    bool WeekStartSelectionRequired
);