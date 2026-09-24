using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class StudentProfileImageCommandHandler(
    IBaseRepository<Student> studentRepository,
    IProfileImageFileStore fileStore,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateStudentProfileImageCommand, Result<StudentDetailsResponse>>,
      IRequestHandler<RemoveStudentProfileImageCommand, Result<StudentDetailsResponse>>
{
    private static readonly HashSet<string> AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    public async Task<Result<StudentDetailsResponse>> Handle(
        UpdateStudentProfileImageCommand request,
        CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetStudentAsync();
        if (studentResult.IsError)
        {
            return studentResult.Errors;
        }

        string extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return Error.Validation(
                "Students.InvalidProfileImage",
                "Profile image must be a JPG, PNG, or WebP image.");
        }

        if (request.FileSizeBytes <= 0 || request.FileSizeBytes > 10 * 1024 * 1024)
        {
            return Error.Validation(
                "Students.ProfileImageSizeInvalid",
                "Profile image must be between 1 byte and 10 MB.");
        }

        string? previousReference = studentResult.Value.ProfileImageStorageReference;
        await using Stream content = await request.OpenReadAsync(cancellationToken);
        string reference = await fileStore.SaveAsync(
            studentResult.Value.Id,
            extension,
            content,
            cancellationToken);

        studentResult.Value.SetProfileImageStorageReference(reference);
        await unitOfWork.SaveChangesAsync();

        if (previousReference is not null)
        {
            await fileStore.DeleteAsync(previousReference, cancellationToken);
        }

        return studentResult.Value.ToDetailsResponse();
    }

    public async Task<Result<StudentDetailsResponse>> Handle(
        RemoveStudentProfileImageCommand request,
        CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetStudentAsync();
        if (studentResult.IsError)
        {
            return studentResult.Errors;
        }

        string? previousReference = studentResult.Value.ProfileImageStorageReference;
        studentResult.Value.SetProfileImageStorageReference(null);
        await unitOfWork.SaveChangesAsync();

        if (previousReference is not null)
        {
            await fileStore.DeleteAsync(previousReference, cancellationToken);
        }

        return studentResult.Value.ToDetailsResponse();
    }

    private async Task<Result<Student>> GetStudentAsync()
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Students.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId,
            item => item.User,
            item => item.Subjects);

        return student is null
            ? Error.NotFound("Students.NotFound", "The student profile was not found.")
            : student;
    }
}
