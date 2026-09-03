using FocusLens.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace FocusLens.Infrastructure.Identity.Seed;

public sealed class UserRoleSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRoleSeeder(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;
    }
}
