namespace FocusLens.Application.Common.Models;

public sealed record GoogleUserInfo(
    string ProviderKey,
    string Email,
    string? FirstName,
    string? LastName,
    bool EmailVerified);