namespace FocusLens.Contracts;

public sealed record UpdateActiveChildRequest(Guid StudentId);

public sealed record ActiveChildResponse(Guid ActiveChildId);
