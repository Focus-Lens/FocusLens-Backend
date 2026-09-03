using FocusLens.Application.Common.Models;

namespace FocusLens.Application.Common.Interfaces;

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default);
}
