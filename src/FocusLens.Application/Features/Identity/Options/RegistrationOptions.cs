namespace FocusLens.Application.Features.Identity.Options;

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    public string TermsVersion { get; set; } = string.Empty;
    public int EmailVerificationCodeLifetimeMinutes { get; init; } = 10;
}
