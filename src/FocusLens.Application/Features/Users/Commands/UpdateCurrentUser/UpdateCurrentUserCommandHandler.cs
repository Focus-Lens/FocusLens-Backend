using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Features.Users.Dtos;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Users.Commands.UpdateCurrentUser;

public sealed class UpdateCurrentUserCommandHandler
    : IRequestHandler<UpdateCurrentUserCommand, Result<UserProfileDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;

    public UpdateCurrentUserCommandHandler(
        ICurrentUser currentUser,
        IIdentityService identityService)
    {
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<Result<UserProfileDto>> Handle(
        UpdateCurrentUserCommand request,
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

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();

        IdentityResultSummary updateResult = await _identityService.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return updateResult.ToApplicationErrors();
        }

        IReadOnlyCollection<string> roles = await _identityService.GetRolesAsync(user);

        return user.ToProfileDto(roles);
    }
}
