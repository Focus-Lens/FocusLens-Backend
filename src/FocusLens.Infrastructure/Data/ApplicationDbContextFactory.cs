using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FocusLens.Infrastructure.Data;

public sealed class ApplicationDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        string appSettingsPath = FindAppSettings();

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(appSettingsPath)!)
            .AddJsonFile("appsettings.json", false)
            .AddUserSecrets("e0087eb7-5390-4b2a-bd51-8b66652060d2")
            .Build();

        string? connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");
        }

        DbContextOptionsBuilder<ApplicationDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string FindAppSettings()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "src",
                "FocusLens.Api",
                "appsettings.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            string currentDirectoryCandidate =
                Path.Combine(directory.FullName, "appsettings.json");

            if (File.Exists(currentDirectoryCandidate))
            {
                return currentDirectoryCandidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate appsettings.json.");
    }
}