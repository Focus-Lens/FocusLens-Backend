using Microsoft.Extensions.Options;

namespace FocusLens.Infrastructure.Authentication;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const int MinimumSecretLength = 32;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            failures.Add("Jwt:Secret is required.");
        }
        else if (options.Secret.Length < MinimumSecretLength)
        {
            failures.Add($"Jwt:Secret must be at least {MinimumSecretLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (options.TokenExpirationInMinutes <= 0)
        {
            failures.Add("Jwt:TokenExpirationInMinutes must be greater than zero.");
        }

        if (options.RefreshTokenExpirationInDays <= 0)
        {
            failures.Add("Jwt:RefreshTokenExpirationInDays must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
