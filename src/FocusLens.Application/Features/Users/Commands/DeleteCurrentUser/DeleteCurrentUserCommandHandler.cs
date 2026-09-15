using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Users.Commands.DeleteCurrentUser;

public sealed class DeleteCurrentUserCommandHandler(
    ICurrentUser currentUser,
    IIdentityService identityService,
    ITokenProvider tokenProvider,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteCurrentUserCommand, Result<Success>>
{
    private static readonly TimeSpan RestoreWindow = TimeSpan.FromDays(30);

    public async Task<Result<Success>> Handle(
        DeleteCurrentUserCommand request,
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

        if (!user.IsDisabled)
        {
            user.SoftDelete(timeProvider.GetUtcNow(), RestoreWindow);
            IdentityResultSummary updateResult = await identityService.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return updateResult.ToApplicationErrors();
            }
        }

        await tokenProvider.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        return Result.Success;
    }
}
