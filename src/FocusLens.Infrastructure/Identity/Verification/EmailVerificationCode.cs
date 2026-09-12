namespace FocusLens.Infrastructure.Identity.Verification;

public sealed class EmailVerificationCode
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public int Purpose { get; set; }

    public DateTimeOffset ExpiresOnUtc { get; set; }

    public DateTimeOffset? UsedOnUtc { get; set; }

    public DateTimeOffset CreatedOnUtc { get; set; }
}