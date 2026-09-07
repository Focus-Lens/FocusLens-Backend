namespace FocusLens.Domain.Common.Interfaces;

public interface IStudyMaterialFileStore
{
    Task<string> SaveOriginalAsync(
        Guid studentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken);

    Task<string> SaveDerivedAsync(
        Guid studentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken);
}
