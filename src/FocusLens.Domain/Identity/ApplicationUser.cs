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

    public DateTimeOffset? TermsAcceptedAtUtc { get; private set; }

    public string? TermsVersion { get; private set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];

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
}
