namespace FocusLens.Contracts.Terms;

public sealed record TermsResponse(
    string Version,
    string Content);