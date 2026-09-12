namespace FocusLens.Contracts.Home;

public sealed record SessionsByDayResponse(IReadOnlyCollection<SessionsByDayItemResponse> Days);

public sealed record SessionsByDayItemResponse(DateOnly Date, int SessionCount);