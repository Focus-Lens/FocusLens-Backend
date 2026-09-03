using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace FocusLens.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ApplicationUser?> FindByIdAsync(Guid userId)
        => await _userManager.FindByIdAsync(userId.ToString());

    public async Task<ApplicationUser?> FindByEmailAsync(string email)
        => await _userManager.FindByEmailAsync(email);

    public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
        => await _userManager.CheckPasswordAsync(user, password);

    public async Task<IReadOnlyCollection<string>> GetRolesAsync(ApplicationUser user)
        => [.. await _userManager.GetRolesAsync(user)];

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string role)
        => await _userManager.IsInRoleAsync(user, role);

    public async Task<IdentityResultSummary> CreateAsync(ApplicationUser user, string password)
    {
        IdentityResult result = await _userManager.CreateAsync(user, password);

        return result.Succeeded
            ? IdentityResultSummary.Success
            : new IdentityResultSummary(
                false,
                [.. result.Errors.Select(error => error.Description)]);
    }
}
