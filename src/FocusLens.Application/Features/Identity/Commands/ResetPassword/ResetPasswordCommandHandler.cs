using FocusLens.Domain.Common.Interfaces;
using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Models;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler
    : IRequestHandler<ResetPasswordCommand, Result<Success>>
{
    private readonly IIdentityService _identityService;
    private readonly IEmailVerificationCodeStore _codeStore;
    private readonly ITokenProvider _tokenProvider;
    private readonly TimeProvider _timeProvider;

    public ResetPasswordCommandHandler(
        IIdentityService identityService,
        IEmailVerificationCodeStore codeStore,
        ITokenProvider tokenProvider,
        TimeProvider timeProvider)
    {
        _identityService = identityService;
        _codeStore = codeStore;
        _tokenProvider = tokenProvider;
        _timeProvider = timeProvider;
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

        EmailVerificationCodeValidationResult validationResult =
            await _codeStore.ValidateAsync(
                request.Email,
                request.Otp,
                _timeProvider.GetUtcNow(),
                cancellationToken,
                OtpCodePurpose.PasswordReset,
                consume: false);

        if (validationResult.Status == EmailVerificationCodeValidationStatus.Expired)
        {
            return ApplicationErrors.Otp.Expired;
        }

        if (validationResult.Status == EmailVerificationCodeValidationStatus.Used)
        {
            return ApplicationErrors.Otp.AlreadyUsed;
        }

        if (validationResult.Status != EmailVerificationCodeValidationStatus.Valid
            || validationResult.UserId != user.Id)
        {
            return ApplicationErrors.Otp.Invalid;
        }

        IdentityResultSummary result = await _identityService.SetPasswordAsync(
            user,
            request.NewPassword);

        if (!result.Succeeded)
        {
            return result.ToApplicationErrors();
        }

        await _tokenProvider.RevokeAllRefreshTokensAsync(
            user.Id,
            cancellationToken);

        await _codeStore.ConsumeAsync(
            request.Email,
            request.Otp,
            _timeProvider.GetUtcNow(),
            cancellationToken,
            OtpCodePurpose.PasswordReset);

        return Result.Success;
    }
}
