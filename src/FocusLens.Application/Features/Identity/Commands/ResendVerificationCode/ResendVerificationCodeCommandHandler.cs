using System.Security.Cryptography;
using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Features.Identity.Options;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.ResendVerificationCode;

public sealed class ResendVerificationCodeCommandHandler
    : IRequestHandler<ResendVerificationCodeCommand, Result<Success>>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private readonly IEmailVerificationCodeStore _codeStore;
    private readonly IEmailSender _emailSender;

    private readonly IIdentityService _identityService;
    private readonly RegistrationOptions _registrationOptions;
    private readonly TimeProvider _timeProvider;

    public ResendVerificationCodeCommandHandler(
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
        ResendVerificationCodeCommand request,
        CancellationToken cancellationToken)
    {
        string email = request.Email.Trim();
        ApplicationUser? user = await _identityService.FindByEmailAsync(email);

        if (user is null)
        {
            return Result.Success;
        }

        if (await _identityService.IsEmailConfirmedAsync(user))
        {
            return ApplicationErrors.Identity.EmailAlreadyConfirmed;
        }

        string code = GenerateCode();

        await _codeStore.SaveAsync(
            user.Id,
            email,
            code,
            _timeProvider.GetUtcNow().Add(CodeLifetime),
            cancellationToken);

        TimeSpan codeLifetime = TimeSpan.FromMinutes(
            _registrationOptions.EmailVerificationCodeLifetimeMinutes);

        await _emailSender.SendEmailVerificationCodeAsync(
            email,
            code,
            codeLifetime,
            cancellationToken);

        return Result.Success;
    }

    private static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}