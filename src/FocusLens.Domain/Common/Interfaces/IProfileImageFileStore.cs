namespace FocusLens.Domain.Common.Interfaces;

public interface IProfileImageFileStore
{
    Task<string> SaveAsync(
        Guid ownerId,
        string extension,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        string storageReference,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string storageReference,
        CancellationToken cancellationToken);
}