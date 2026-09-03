using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler
    : IRequestHandler<ForgotPasswordCommand, Result<Success>>
{
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;

    public ForgotPasswordCommandHandler(
        IIdentityService identityService,
        IEmailSender emailSender)
    {
        _identityService = identityService;
        _emailSender = emailSender;
    }

    public async Task<Result<Success>> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _identityService.FindByEmailAsync(
            request.Email.Trim());

        if (user is null || user.IsDisabled)
        {
            return Result.Success;
        }

        string token = await _identityService.GeneratePasswordResetTokenAsync(user);

        await _emailSender.SendPasswordResetAsync(
            user.Email ?? request.Email.Trim(),
            token,
            cancellationToken);

        return Result.Success;
    }
}
