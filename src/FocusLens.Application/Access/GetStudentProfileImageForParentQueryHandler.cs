using FocusLens.Application.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Access;

public sealed class GetStudentProfileImageForParentQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IProfileImageFileStore fileStore,
    ICurrentUser currentUser)
    : IRequestHandler<GetStudentProfileImageForParentQuery, Result<StudentProfileImageResult>>
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp"
    };

    public async Task<Result<StudentProfileImageResult>> Handle(
        GetStudentProfileImageForParentQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized("Parents.CurrentUserUnavailable", "The current user could not be identified.");
        }

        if (request.StudentId == Guid.Empty)
        {
            return Error.Validation("Students.InvalidStudentId", "Student ID is required.");
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);
        if (parent is null || await relationshipRepository.FirstOrDefaultAsync(relationship =>
                relationship.ParentId == parent.Id && relationship.StudentId == request.StudentId &&
                relationship.Status == RelationshipStatus.Active) is null)
        {
            return Error.NotFound("Students.NotFound", "The requested student is not available.");
        }

        Student? student = await studentRepository.GetByIdAsync(request.StudentId);
        if (student is null || string.IsNullOrWhiteSpace(student.ProfileImageStorageReference))
        {
            return new StudentProfileImageResult(null);
        }

        if (!ContentTypes.TryGetValue(Path.GetExtension(student.ProfileImageStorageReference), out string? contentType))
        {
            return Error.NotFound("Students.ProfileImageNotFound", "The student profile image could not be found.");
        }

        try
        {
            Stream content = await fileStore.OpenReadAsync(student.ProfileImageStorageReference, cancellationToken);
            return new StudentProfileImageResult(new StudentProfileImageFile(content, contentType));
        }
        catch (FileNotFoundException)
        {
            return Error.NotFound("Students.ProfileImageNotFound", "The student profile image could not be found.");
        }
        catch (DirectoryNotFoundException)
        {
            return Error.NotFound("Students.ProfileImageNotFound", "The student profile image could not be found.");
        }
    }
}
