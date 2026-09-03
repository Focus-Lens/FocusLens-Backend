using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Identity;

namespace FocusLens.Domain.Interfaces;

public interface IIdentityService
{
    Task<ApplicationUser?> FindByIdAsync(Guid userId);

    Task<ApplicationUser?> FindByEmailAsync(string email);

    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);

    Task<bool> IsEmailConfirmedAsync(ApplicationUser user);

    Task<bool> IsLockedOutAsync(ApplicationUser user);

    Task<IReadOnlyCollection<string>> GetRolesAsync(ApplicationUser user);

    Task<bool> IsInRoleAsync(ApplicationUser user, string role);

    Task<IdentityResultSummary> CreateAsync(ApplicationUser user, string password);

    Task<IdentityResultSummary> UpdateAsync(ApplicationUser user);

    Task<IdentityResultSummary> AddToRoleAsync(ApplicationUser user, string role);

    Task<IdentityResultSummary> ConfirmEmailAsync(ApplicationUser user);

    Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user);

    Task<IdentityResultSummary> ResetPasswordAsync(
        ApplicationUser user,
        string token,
        string newPassword);

    Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey);

    Task<IdentityResultSummary> AddLoginAsync(
        ApplicationUser user,
        string loginProvider,
        string providerKey,
        string displayName);
}
