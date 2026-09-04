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
        Console.WriteLine("========== GOOGLE VALIDATION ==========");
        Console.WriteLine($"ClientId configured: {!string.IsNullOrWhiteSpace(_options.ClientId)}");
        Console.WriteLine($"ClientId: {_options.ClientId}");
        Console.WriteLine($"Token received: {!string.IsNullOrWhiteSpace(idToken)}");
        Console.WriteLine($"Token length: {idToken?.Length}");
        Console.WriteLine("=======================================");

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
        catch (InvalidJwtException ex)
        {
            Console.WriteLine("========== GOOGLE TOKEN ERROR ==========");
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex);
            Console.WriteLine("========================================");

            return null;
        }
    }
}
