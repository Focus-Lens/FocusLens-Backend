using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FocusLens.Infrastructure.Data;

public sealed class ApplicationDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDBContext>
{
    public ApplicationDBContext CreateDbContext(string[] args)
    {
        string? connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            string appSettingsPath = FindAppSettings();

            using FileStream stream = File.OpenRead(appSettingsPath);
            using JsonDocument document = JsonDocument.Parse(stream);

            if (document.RootElement.TryGetProperty(
                    "ConnectionStrings",
                    out JsonElement connectionStrings)
                && connectionStrings.TryGetProperty(
                    "DefaultConnection",
                    out JsonElement defaultConnection)
                && defaultConnection.GetString() is { Length: > 0 } value)
            {
                connectionString = value;
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");
        }

        DbContextOptionsBuilder<ApplicationDBContext> optionsBuilder = new();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDBContext(optionsBuilder.Options);
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
