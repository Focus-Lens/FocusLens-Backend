using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Features.Users.Dtos;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Users.Queries.GetCurrentUserAccountStatus;

public sealed class GetCurrentUserAccountStatusQueryHandler(
    ICurrentUser currentUser,
    IIdentityService identityService,
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository)
    : IRequestHandler<GetCurrentUserAccountStatusQuery, Result<UserAccountStatusDto>>
{
    public async Task<Result<UserAccountStatusDto>> Handle(
        GetCurrentUserAccountStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return ApplicationErrors.Users.CurrentUserUnavailable;
        }

        ApplicationUser? user = await identityService.FindByIdAsync(userId);

        if (user is null)
        {
            return ApplicationErrors.Users.NotFound;
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            parent => parent.UserId == userId);

        IReadOnlyCollection<UserChildRelationshipStatusDto> childRelationships =
            parent is null
                ? []
                : await GetChildRelationshipsAsync(parent.Id);

        return new UserAccountStatusDto(
            user.EmailConfirmed,
            user.EmailConfirmed ? "Verified" : "Unverified",
            user.IsDisabled,
            user.IsDisabled ? "Disabled" : "Active",
            childRelationships);
    }

    private async Task<IReadOnlyCollection<UserChildRelationshipStatusDto>> GetChildRelationshipsAsync(
        Guid parentId)
    {
        List<ParentStudentRelationship> relationships =
            [.. await relationshipRepository.GetAllAsync(
                relationship => relationship.ParentId == parentId)];

        if (relationships.Count == 0)
        {
            return [];
        }

        Guid[] studentIds = relationships
            .Select(relationship => relationship.StudentId)
            .Distinct()
            .ToArray();

        IEnumerable<Student> students = await studentRepository.GetAllAsync(
            student => studentIds.Contains(student.Id),
            student => student.User);

        IReadOnlyDictionary<Guid, Student> studentsById =
            students.ToDictionary(student => student.Id);

        return relationships
            .Select(relationship =>
            {
                studentsById.TryGetValue(relationship.StudentId, out Student? student);

                return new UserChildRelationshipStatusDto(
                    relationship.Id,
                    relationship.StudentId,
                    GetChildName(student),
                    relationship.Status.ToString());
            })
            .ToList();
    }

    private static string GetChildName(Student? student)
    {
        if (student is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(student.PreferredName))
        {
            return student.PreferredName;
        }

        return string.Join(
            " ",
            new[] { student.User.FirstName, student.User.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
