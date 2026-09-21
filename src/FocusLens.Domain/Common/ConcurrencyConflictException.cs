namespace FocusLens.Domain.Common;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Exception innerException)
        : base("The requested operation conflicted with another update.", innerException) { }
}
