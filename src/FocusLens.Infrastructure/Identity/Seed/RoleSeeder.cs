using FocusLens.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace FocusLens.Infrastructure.Identity.Seed;

public sealed class RoleSeeder
{
    private readonly RoleManager<ApplicationRole> _roleManager;

    public RoleSeeder(RoleManager<ApplicationRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (string roleName in DefaultRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ApplicationRole? role = await _roleManager.FindByNameAsync(roleName);

            if (role is null)
            {
                role = new ApplicationRole
                {
                    Name = roleName,
                    NormalizedName = _roleManager.NormalizeKey(roleName),
                    IsDefault = false,
                    IsDeleted = false
                };

                IdentityResult createResult = await _roleManager.CreateAsync(role);
                ThrowIfFailed(createResult, $"Failed to create role '{roleName}'.");

                continue;
            }

            bool hasChanges = false;

            if (role.Name != roleName)
            {
                role.Name = roleName;
                hasChanges = true;
            }

            string normalizedName = _roleManager.NormalizeKey(roleName);

            if (role.NormalizedName != normalizedName)
            {
                role.NormalizedName = normalizedName;
                hasChanges = true;
            }

            if (hasChanges)
            {
                IdentityResult updateResult = await _roleManager.UpdateAsync(role);
                ThrowIfFailed(updateResult, $"Failed to update role '{roleName}'.");
            }
        }
    }

    private static void ThrowIfFailed(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        string errors = string.Join(
            Environment.NewLine,
            result.Errors.Select(error => $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException($"{message}{Environment.NewLine}{errors}");
    }
}
