using FocusLens.Application.Common.Errors;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Users.Commands.RestoreAccount;

public sealed class RestoreAccountCommandHandler(
    IIdentityService identityService,
    TimeProvider timeProvider)
    : IRequestHandler<RestoreAccountCommand, Result<Success>>
{
    public async Task<Result<Success>> Handle(
        RestoreAccountCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await identityService.FindByEmailAsync(request.Email.Trim());
        if (user is null ||
            !user.IsDisabled ||
            !user.CanSelfRestore(timeProvider.GetUtcNow()) ||
            !await identityService.CheckPasswordAsync(user, request.Password))
        {
            return ApplicationErrors.Identity.InvalidCredentials;
        }

        user.Restore();
        IdentityResultSummary result = await identityService.UpdateAsync(user);
        return result.Succeeded
            ? Result.Success
            : result.ToApplicationErrors();
    }
}