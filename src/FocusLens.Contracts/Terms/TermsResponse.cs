namespace FocusLens.Contracts.Terms;

public sealed record TermsResponse(
    Guid Id,
    string Audience,
    string Version,
    string Content,
    DateTimeOffset PublishedOnUtc);
