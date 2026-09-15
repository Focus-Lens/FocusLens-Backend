namespace FocusLens.Application.Features.Users.Dtos;

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool EmailConfirmed,
    bool IsDisabled,
    IReadOnlyCollection<string> Roles);
