using FocusLens.Domain.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FocusLens.Infrastructure.StudySessions;

public sealed class LocalStudyMaterialFileStore(
    IOptions<StudyMaterialStorageOptions> options,
    IHostEnvironment environment) : IStudyMaterialFileStore
{
    private readonly string _rootPath = ResolveRootPath(options.Value.RootPath, environment.ContentRootPath);

    public Task<string> SaveOriginalAsync(
        Guid studentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken) =>
        SaveAsync(studentId, "original", fileName, content, cancellationToken);

    public Task<string> SaveDerivedAsync(
        Guid studentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken) =>
        SaveAsync(studentId, "derived", fileName, content, cancellationToken);

    public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken)
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

    private async Task<string> SaveAsync(
        Guid studentId,
        string category,
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(fileName);
        string relativePath =
            Path.Combine(studentId.ToString("N"), category, $"{Guid.CreateVersion7():N}{extension}");
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

    private string ToFullPath(string storageReference)
    {
        string normalizedReference = storageReference.Replace('/', Path.DirectorySeparatorChar);
        string root = Path.GetFullPath(_rootPath);
        string path = Path.GetFullPath(Path.Combine(root, normalizedReference));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(path, root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The storage reference is outside the material storage root.");
        }

        return path;
    }

    private static string ResolveRootPath(string? configuredRootPath, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRootPath))
        {
            throw new InvalidOperationException("StudyMaterialStorage:RootPath must be configured.");
        }

        return Path.IsPathRooted(configuredRootPath)
            ? Path.GetFullPath(configuredRootPath)
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredRootPath));
    }
}