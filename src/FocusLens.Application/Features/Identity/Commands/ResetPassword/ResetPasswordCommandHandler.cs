using FocusLens.Domain.Common.Interfaces;
using FocusLens.Application.Common.Errors;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler
    : IRequestHandler<ResetPasswordCommand, Result<Success>>
{
    private readonly IIdentityService _identityService;

    public ResetPasswordCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result<Success>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _identityService.FindByEmailAsync(
            request.Email.Trim());

        if (user is null || user.IsDisabled)
        {
            return ApplicationErrors.Identity.InvalidCredentials;
        }

        IdentityResultSummary result = await _identityService.ResetPasswordAsync(
            user,
            request.Token,
            request.NewPassword);

        return result.Succeeded
            ? Result.Success
            : result.ToApplicationErrors();
    }
}
