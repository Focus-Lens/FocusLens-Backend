using System.Security.Cryptography;

using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Models;
using FocusLens.Application.Features.Identity.Options;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler
    : IRequestHandler<ForgotPasswordCommand, Result<Success>>
{
    private readonly IIdentityService _identityService;
    private readonly IEmailVerificationCodeStore _codeStore;
    private readonly IEmailSender _emailSender;
    private readonly TimeProvider _timeProvider;
    private readonly RegistrationOptions _registrationOptions;

    public ForgotPasswordCommandHandler(
        IIdentityService identityService,
        IEmailVerificationCodeStore codeStore,
        IEmailSender emailSender,
        TimeProvider timeProvider,
        RegistrationOptions registrationOptions)
    {
        _identityService = identityService;
        _codeStore = codeStore;
        _emailSender = emailSender;
        _timeProvider = timeProvider;
        _registrationOptions = registrationOptions;
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

        string code = GenerateCode();
        TimeSpan codeLifetime = TimeSpan.FromMinutes(
            _registrationOptions.EmailVerificationCodeLifetimeMinutes);

        await _codeStore.SaveAsync(
            user.Id,
            user.Email ?? request.Email.Trim(),
            code,
            _timeProvider.GetUtcNow().Add(codeLifetime),
            cancellationToken,
            OtpCodePurpose.PasswordReset);

        await _emailSender.SendPasswordResetAsync(
            user.Email ?? request.Email.Trim(),
            code,
            codeLifetime,
            cancellationToken);

        return Result.Success;
    }

    private static string GenerateCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
