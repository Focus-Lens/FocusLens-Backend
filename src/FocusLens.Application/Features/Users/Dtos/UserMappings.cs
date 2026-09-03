using FocusLens.Domain.Identity;

namespace FocusLens.Application.Features.Users.Dtos;

internal static class UserMappings
{
    public static UserProfileDto ToProfileDto(
        this ApplicationUser user,
        IReadOnlyCollection<string> roles)
        => new(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            roles);
}
