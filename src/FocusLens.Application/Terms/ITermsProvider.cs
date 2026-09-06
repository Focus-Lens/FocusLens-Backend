using FocusLens.Contracts.Terms;

namespace FocusLens.Application.Terms;

public interface ITermsProvider
{
    TermsResponse GetCurrentTerms();
}
