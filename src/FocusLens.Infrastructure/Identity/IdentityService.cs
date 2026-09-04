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

    public async Task<bool> IsEmailConfirmedAsync(ApplicationUser user)
        => await _userManager.IsEmailConfirmedAsync(user);

    public async Task<bool> IsLockedOutAsync(ApplicationUser user)
        => await _userManager.IsLockedOutAsync(user);

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

    public async Task<IdentityResultSummary> AddToRoleAsync(
        ApplicationUser user,
        string role)
        => ToSummary(await _userManager.AddToRoleAsync(user, role));

    public async Task<IdentityResultSummary> UpdateAsync(ApplicationUser user)
        => ToSummary(await _userManager.UpdateAsync(user));

    public async Task<IdentityResultSummary> ConfirmEmailAsync(ApplicationUser user)
    {
        if (user.EmailConfirmed)
        {
            return IdentityResultSummary.Success;
        }

        string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        return ToSummary(await _userManager.ConfirmEmailAsync(user, token));
    }

    public async Task<IdentityResultSummary> SetPasswordAsync(
        ApplicationUser user,
        string newPassword)
    {
        foreach (IPasswordValidator<ApplicationUser> validator in _userManager.PasswordValidators)
        {
            IdentityResult validationResult = await validator.ValidateAsync(
                _userManager,
                user,
                newPassword);

            if (!validationResult.Succeeded)
            {
                return ToSummary(validationResult);
            }
        }

        user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.CreateVersion7().ToString();

        return ToSummary(await _userManager.UpdateAsync(user));
    }

    public async Task<IdentityResultSummary> ChangePasswordAsync(
        ApplicationUser user,
        string currentPassword,
        string newPassword)
        => ToSummary(await _userManager.ChangePasswordAsync(
            user,
            currentPassword,
            newPassword));

    public async Task<ApplicationUser?> FindByLoginAsync(
        string loginProvider,
        string providerKey)
        => await _userManager.FindByLoginAsync(loginProvider, providerKey);

    public async Task<IdentityResultSummary> AddLoginAsync(
        ApplicationUser user,
        string loginProvider,
        string providerKey,
        string displayName)
        => ToSummary(await _userManager.AddLoginAsync(
            user,
            new UserLoginInfo(loginProvider, providerKey, displayName)));

    private static IdentityResultSummary ToSummary(IdentityResult result)
        => result.Succeeded
            ? IdentityResultSummary.Success
            : new IdentityResultSummary(
                false,
                [.. result.Errors.Select(error => error.Description)]);
}
