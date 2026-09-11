namespace FocusLens.Contracts.Terms;

public sealed record TermsDecisionResponse(
    bool Accepted,
    Guid TermsId,
    string Audience,
    string Version,
    DateTimeOffset? AcceptedAtUtc);
