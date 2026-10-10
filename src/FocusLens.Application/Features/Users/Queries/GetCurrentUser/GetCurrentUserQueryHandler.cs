using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Features.Users.Dtos;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Users.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler
    : IRequestHandler<GetCurrentUserQuery, Result<UserProfileDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;
    private readonly IBaseRepository<Parent> _parents;
    private readonly IBaseRepository<Student> _students;

    public GetCurrentUserQueryHandler(
        ICurrentUser currentUser,
        IIdentityService identityService,
        IBaseRepository<Parent> parents,
        IBaseRepository<Student> students)
    {
        _currentUser = currentUser;
        _identityService = identityService;
        _parents = parents;
        _students = students;
    }

    public async Task<Result<UserProfileDto>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return ApplicationErrors.Users.CurrentUserUnavailable;
        }

        ApplicationUser? user = await _identityService.FindByIdAsync(userId);

        if (user is null)
        {
            return ApplicationErrors.Users.NotFound;
        }

        IReadOnlyCollection<string> roles = await _identityService.GetRolesAsync(user);

        Parent? parent = roles.Contains(ApplicationRoles.Parent, StringComparer.Ordinal)
            ? await _parents.FirstOrDefaultAsync(item => item.UserId == userId)
            : null;
        Student? student = roles.Contains(ApplicationRoles.Student, StringComparer.Ordinal)
            ? await _students.FirstOrDefaultAsync(item => item.UserId == userId)
            : null;

        return user.ToProfileDto(roles, parent, student);
    }
}