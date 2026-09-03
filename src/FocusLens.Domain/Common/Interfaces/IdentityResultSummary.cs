namespace FocusLens.Domain.Interfaces;

public sealed record IdentityResultSummary(
    bool Succeeded,
    IReadOnlyCollection<string> Errors)
{
    public static IdentityResultSummary Success { get; } = new(true, []);
}
