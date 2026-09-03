using System.Security.Cryptography;

using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.Register;

public sealed class RegisterCommandHandler
    : IRequestHandler<RegisterCommand, Result<Success>>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IIdentityService _identityService;
    private readonly IEmailVerificationCodeStore _codeStore;
    private readonly IEmailSender _emailSender;
    private readonly TimeProvider _timeProvider;

    public RegisterCommandHandler(
        IIdentityService identityService,
        IEmailVerificationCodeStore codeStore,
        IEmailSender emailSender,
        TimeProvider timeProvider)
    {
        _identityService = identityService;
        _codeStore = codeStore;
        _emailSender = emailSender;
        _timeProvider = timeProvider;
    }

    public async Task<Result<Success>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        string email = request.Email.Trim();

        if (await _identityService.FindByEmailAsync(email) is not null)
        {
            return ApplicationErrors.Identity.EmailAlreadyRegistered;
        }

        ApplicationUser user = new()
        {
            Email = email,
            UserName = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = false
        };

        IdentityResultSummary createResult = await _identityService.CreateAsync(
            user,
            request.Password);

        if (!createResult.Succeeded)
        {
            return createResult.ToApplicationErrors();
        }

        IdentityResultSummary roleResult = await _identityService.AddToRoleAsync(
            user,
            ApplicationRoles.Student);

        if (!roleResult.Succeeded)
        {
            return roleResult.ToApplicationErrors();
        }

        string code = GenerateCode();

        await _codeStore.SaveAsync(
            user.Id,
            email,
            code,
            _timeProvider.GetUtcNow().Add(CodeLifetime),
            cancellationToken);

        await _emailSender.SendEmailVerificationCodeAsync(
            email,
            code,
            cancellationToken);

        return Result.Success;
    }

    private static string GenerateCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
