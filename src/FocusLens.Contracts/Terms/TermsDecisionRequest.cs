namespace FocusLens.Contracts.Terms;

public sealed record TermsDecisionRequest(
    Guid? TermsId,
    bool? Accepted);
