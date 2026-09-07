using FocusLens.Domain.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FocusLens.Infrastructure.StudySessions;

public sealed class LocalStudyMaterialFileStore(IConfiguration configuration) : IStudyMaterialFileStore
{
    private readonly string _rootPath = configuration["StudyMaterialStorage:RootPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "study-materials");

    public Task<string> SaveOriginalAsync(
        Guid studentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
        => SaveAsync(studentId, "original", fileName, content, cancellationToken);

    public Task<string> SaveDerivedAsync(
        Guid studentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
        => SaveAsync(studentId, "derived", fileName, content, cancellationToken);

    public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken)
    {
        string path = ToFullPath(storageReference);
        Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    private async Task<string> SaveAsync(
        Guid studentId,
        string category,
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(fileName);
        string relativePath = Path.Combine(studentId.ToString("N"), category, $"{Guid.CreateVersion7():N}{extension}");
        string path = ToFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using FileStream output = File.Create(path);
        await content.CopyToAsync(output, cancellationToken);
        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
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
}
