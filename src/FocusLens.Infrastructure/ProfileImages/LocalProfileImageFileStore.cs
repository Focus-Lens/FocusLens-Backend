using FocusLens.Domain.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FocusLens.Infrastructure.ProfileImages;

public sealed class LocalProfileImageFileStore(
    IOptions<ProfileImageStorageOptions> options,
    IHostEnvironment environment
) : IProfileImageFileStore
{
    private readonly string _rootPath = ResolveRootPath(
        options.Value.RootPath,
        environment.ContentRootPath
    );

    public async Task<string> SaveAsync(
        Guid ownerId,
        string extension,
        Stream content,
        CancellationToken cancellationToken
    )
    {
        string safeExtension = extension.StartsWith(".") ? extension : $".{extension}";

        string relativePath = Path.Combine(
            ownerId.ToString("N"),
            $"{Guid.CreateVersion7():N}{safeExtension}"
        );

        string path = ToFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            await using FileStream output = File.Create(path);
            await content.CopyToAsync(output, cancellationToken);

            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            throw;
        }
    }

    public Task<Stream> OpenReadAsync(
        string storageReference,
        CancellationToken cancellationToken)
    {
        string path = ToFullPath(storageReference);
        Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageReference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string path = ToFullPath(storageReference);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ToFullPath(string storageReference)
    {
        string normalizedReference = storageReference.Replace('/', Path.DirectorySeparatorChar);

        string root = Path.GetFullPath(_rootPath);
        string path = Path.GetFullPath(Path.Combine(root, normalizedReference));

        if (
            !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(path, root, StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "The storage reference is outside the profile image storage root."
            );
        }

        return path;
    }

    private static string ResolveRootPath(string? configuredRootPath, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRootPath))
        {
            throw new InvalidOperationException("ProfileImageStorage:RootPath must be configured.");
        }

        return Path.IsPathRooted(configuredRootPath)
            ? Path.GetFullPath(configuredRootPath)
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredRootPath));
    }
}
