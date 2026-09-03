using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Models;
using FocusLens.Infrastructure.Authentication;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace FocusLens.API.Infrastructure;

public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleAuthOptions _options;

    public GoogleTokenValidator(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            return null;
        }

        try
        {
            GoogleJsonWebSignature.Payload payload =
                await GoogleJsonWebSignature.ValidateAsync(
                    idToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = [_options.ClientId]
                    });

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.GivenName,
                payload.FamilyName,
                payload.EmailVerified);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
