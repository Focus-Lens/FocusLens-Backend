using FocusLens.Domain.Identity;
using FocusLens.Domain;

namespace FocusLens.Application.Features.Users.Dtos;

internal static class UserMappings
{
    public static UserProfileDto ToProfileDto(
        this ApplicationUser user,
        IReadOnlyCollection<string> roles,
        Parent? parent = null,
        Student? student = null)
    {
        return new UserProfileDto(
            user.Id,
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.EmailConfirmed,
            user.CreatedAtUtc,
            parent?.WeekStartsOn ?? student?.WeekStartsOn,
            roles.Contains("Parent", StringComparer.Ordinal)
                ? parent?.WeekStartsOn is null
                : roles.Contains("Student", StringComparer.Ordinal) && student?.WeekStartsOn is null);
    }
}
