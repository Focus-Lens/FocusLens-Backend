using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudyMaterial : AuditableEntity
{
    private StudyMaterial()
    {
    }

    private StudyMaterial(
        Guid id,
        Guid studentId,
        string fileName,
        long fileSizeBytes,
        int pageCount,
        string storageReference,
        StudyMaterialSource source)
        : base(id)
    {
        StudentId = studentId;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        PageCount = pageCount;
        StorageReference = storageReference;
        Source = source;
    }

    public Guid StudentId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }

    public int PageCount { get; private set; }

    public string StorageReference { get; private set; } = string.Empty;

    public StudyMaterialSource Source { get; private set; }

    public static Result<StudyMaterial> Create(
        Guid studentId,
        string? fileName,
        long fileSizeBytes,
        int pageCount,
        string? storageReference,
        StudyMaterialSource source)
    {
        if (studentId == Guid.Empty)
        {
            return StudySessionErrors.StudentIdRequired;
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Error.Validation("StudyMaterials.FileNameRequired", "File name is required.");
        }

        if (fileSizeBytes <= 0)
        {
            return Error.Validation("StudyMaterials.FileSizeInvalid", "File size must be greater than zero.");
        }

        if (pageCount <= 0)
        {
            return Error.Validation("StudyMaterials.PageCountInvalid", "Page count must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(storageReference))
        {
            return Error.Validation("StudyMaterials.StorageReferenceRequired", "Storage reference is required.");
        }

        if (!Enum.IsDefined(source))
        {
            return Error.Validation("StudyMaterials.SourceInvalid", "Material source is invalid.");
        }

        return new StudyMaterial(
            Guid.CreateVersion7(),
            studentId,
            fileName.Trim(),
            fileSizeBytes,
            pageCount,
            storageReference.Trim(),
            source);
    }

}
