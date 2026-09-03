using FocusLens.Domain.Common.Interfaces;
using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Models;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.VerifyEmail;

public sealed class VerifyEmailCommandHandler
    : IRequestHandler<VerifyEmailCommand, Result<Success>>
{
    private readonly IIdentityService _identityService;
    private readonly IEmailVerificationCodeStore _codeStore;
    private readonly TimeProvider _timeProvider;

    public VerifyEmailCommandHandler(
        IIdentityService identityService,
        IEmailVerificationCodeStore codeStore,
        TimeProvider timeProvider)
    {
        _identityService = identityService;
        _codeStore = codeStore;
        _timeProvider = timeProvider;
    }

    public async Task<Result<Success>> Handle(
        VerifyEmailCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _identityService.FindByEmailAsync(
            request.Email.Trim());

        if (user is null)
        {
            return ApplicationErrors.Otp.Invalid;
        }

        if (await _identityService.IsEmailConfirmedAsync(user))
        {
            return ApplicationErrors.Identity.EmailAlreadyConfirmed;
        }

        EmailVerificationCodeValidationResult validationResult =
            await _codeStore.ValidateAsync(
                request.Email.Trim(),
                request.Otp,
                _timeProvider.GetUtcNow(),
                cancellationToken);

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

        IdentityResultSummary confirmResult = await _identityService.ConfirmEmailAsync(user);

        return confirmResult.Succeeded
            ? Result.Success
            : confirmResult.ToApplicationErrors();
    }
}
