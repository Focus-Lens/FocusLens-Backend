using FocusLens.Infrastructure.Identity.Seed;
using Microsoft.EntityFrameworkCore;

namespace FocusLens.Infrastructure.Data;

public sealed class ApplicationDbContextInitialiser
{
    private readonly ApplicationDbContext _dbContext;
    private readonly RoleSeeder _roleSeeder;
    private readonly UserRoleSeeder _userRoleSeeder;
    private readonly UserSeeder _userSeeder;

    public ApplicationDbContextInitialiser(
        ApplicationDbContext dbContext,
        RoleSeeder roleSeeder,
        UserSeeder userSeeder,
        UserRoleSeeder userRoleSeeder
    )
    {
        _dbContext = dbContext;
        _roleSeeder = roleSeeder;
        _userSeeder = userSeeder;
        _userRoleSeeder = userRoleSeeder;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsRelational())
        {
            await _dbContext.Database.MigrateAsync(cancellationToken);
        }

        await _roleSeeder.SeedAsync(cancellationToken);
        await _userSeeder.SeedAsync(cancellationToken);
        await _userRoleSeeder.SeedAsync(cancellationToken);
    }
}
