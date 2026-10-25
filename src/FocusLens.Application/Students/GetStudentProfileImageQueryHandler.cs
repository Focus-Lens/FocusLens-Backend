using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class GetStudentProfileImageQueryHandler(
    IBaseRepository<Student> studentRepository,
    IProfileImageFileStore fileStore,
    ICurrentUser currentUser)
    : IRequestHandler<GetStudentProfileImageQuery, Result<StudentProfileImageResult>>
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp"
    };

    public async Task<Result<StudentProfileImageResult>> Handle(
        GetStudentProfileImageQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Students.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);

        if (student is null)
        {
            return Error.NotFound(
                "Students.NotFound",
                "The student profile was not found.");
        }

        if (string.IsNullOrWhiteSpace(student.ProfileImageStorageReference))
        {
            return new StudentProfileImageResult(null);
        }

        string extension = Path.GetExtension(student.ProfileImageStorageReference);

        if (!ContentTypes.TryGetValue(extension, out string? contentType))
        {
            return Error.NotFound(
                "Students.ProfileImageNotFound",
                "The student profile image could not be found.");
        }

        try
        {
            Stream content = await fileStore.OpenReadAsync(
                student.ProfileImageStorageReference,
                cancellationToken);

            return new StudentProfileImageResult(new StudentProfileImageFile(content, contentType));
        }
        catch (FileNotFoundException)
        {
            return Error.NotFound(
                "Students.ProfileImageNotFound",
                "The student profile image could not be found.");
        }
        catch (DirectoryNotFoundException)
        {
            return Error.NotFound(
                "Students.ProfileImageNotFound",
                "The student profile image could not be found.");
        }
    }
}
