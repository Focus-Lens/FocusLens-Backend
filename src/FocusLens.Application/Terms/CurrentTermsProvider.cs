using FocusLens.Application.Features.Identity.Options;
using FocusLens.Contracts.Terms;

namespace FocusLens.Application.Terms;

public sealed class CurrentTermsProvider(RegistrationOptions registrationOptions)
    : ITermsProvider
{
    private const string CurrentContent =
        """
        FocusLens Terms & Privacy

        FocusLens uses account, profile, and learning preference information to provide the application experience, secure user access, and support parent/student relationships. By continuing, you agree to use FocusLens responsibly and understand that the app experience is controlled by the client application after your decision.
        """;

    public TermsResponse GetCurrentTerms() => new(registrationOptions.TermsVersion, CurrentContent);
}