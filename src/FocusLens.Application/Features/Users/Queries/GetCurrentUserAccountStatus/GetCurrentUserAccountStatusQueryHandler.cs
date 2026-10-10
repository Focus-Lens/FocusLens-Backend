using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Features.Users.Dtos;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Users.Queries.GetCurrentUserAccountStatus;

public sealed class GetCurrentUserAccountStatusQueryHandler(
    ICurrentUser currentUser,
    IIdentityService identityService)
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

        return new UserAccountStatusDto(
            user.EmailConfirmed,
            user.EmailConfirmed ? "Verified" : "Unverified",
            user.IsDisabled,
            user.IsDisabled ? "Disabled" : "Active");
    }
}