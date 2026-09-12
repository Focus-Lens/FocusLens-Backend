namespace FocusLens.Domain.Common.Interfaces;

public interface IStudySessionImageFileStore
{
    Task<string> SaveAsync(
        Guid studentId,
        string extension,
        Stream content,
        CancellationToken cancellationToken);

    Task DeleteAsync(string storageReference, CancellationToken cancellationToken);
}
