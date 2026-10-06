using Microsoft.AspNetCore.Identity;

namespace FocusLens.Domain.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.CreateVersion7().ToString();
    }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public bool IsDisabled { get; set; }

    // Identity users are not AuditableEntity instances; retain their account
    // creation instant here for the common profile endpoint.
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public DateTimeOffset? RestoreUntilUtc { get; private set; }

    public DateTimeOffset? TermsAcceptedAtUtc { get; private set; }

    public string? TermsVersion { get; private set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];

    public List<UserTermsAcceptance> TermsAcceptances { get; private set; } = [];

    public void AcceptTerms(string termsVersion, DateTimeOffset acceptedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(termsVersion))
        {
            throw new ArgumentException(
                "Terms version is required.",
                nameof(termsVersion));
        }

        TermsVersion = termsVersion.Trim();
        TermsAcceptedAtUtc = acceptedAtUtc;
    }

    public void SoftDelete(DateTimeOffset deletedAtUtc, TimeSpan restoreWindow)
    {
        IsDisabled = true;
        DeletedAtUtc = deletedAtUtc;
        RestoreUntilUtc = deletedAtUtc.Add(restoreWindow);
    }

    public bool CanSelfRestore(DateTimeOffset utcNow) =>
        DeletedAtUtc is not null &&
        RestoreUntilUtc is not null &&
        utcNow <= RestoreUntilUtc.Value;

    public void Restore()
    {
        IsDisabled = false;
        DeletedAtUtc = null;
        RestoreUntilUtc = null;
    }
}
