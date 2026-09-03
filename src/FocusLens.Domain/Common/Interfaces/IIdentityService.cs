using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Identity;

namespace FocusLens.Domain.Interfaces;

public interface IIdentityService
{
    Task<ApplicationUser?> FindByIdAsync(Guid userId);

    Task<ApplicationUser?> FindByEmailAsync(string email);

    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);

    Task<IReadOnlyCollection<string>> GetRolesAsync(ApplicationUser user);

    Task<bool> IsInRoleAsync(ApplicationUser user, string role);

    Task<IdentityResultSummary> CreateAsync(ApplicationUser user, string password);
}
