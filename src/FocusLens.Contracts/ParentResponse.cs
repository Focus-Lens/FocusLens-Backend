namespace FocusLens.Contracts;

public sealed record ParentResponse(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    DayOfWeek? WeekStartsOn,
    bool WeekStartSelectionRequired
);
