using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Users.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler
    : IRequestHandler<ChangePasswordCommand, Result<Success>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;

    public ChangePasswordCommandHandler(
        ICurrentUser currentUser,
        IIdentityService identityService)
    {
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<Result<Success>> Handle(
        ChangePasswordCommand request,
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

        if (!await _identityService.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return ApplicationErrors.Identity.InvalidCredentials;
        }

        IdentityResultSummary result = await _identityService.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        return result.Succeeded
            ? Result.Success
            : result.ToApplicationErrors();
    }
}