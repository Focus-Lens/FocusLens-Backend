using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Models;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.GoogleLogin;

public sealed class GoogleLoginCommandHandler
    : IRequestHandler<GoogleLoginCommand, Result<AuthResponse>>
{
    private const string LoginProvider = "Google";

    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IIdentityService _identityService;
    private readonly ITokenProvider _tokenProvider;

    public GoogleLoginCommandHandler(
        IGoogleTokenValidator googleTokenValidator,
        IIdentityService identityService,
        ITokenProvider tokenProvider)
    {
        _googleTokenValidator = googleTokenValidator;
        _identityService = identityService;
        _tokenProvider = tokenProvider;
    }

    public async Task<Result<AuthResponse>> Handle(
        GoogleLoginCommand request,
        CancellationToken cancellationToken)
    {
        GoogleUserInfo? googleUser = await _googleTokenValidator.ValidateAsync(
            request.IdToken,
            cancellationToken);

        if (googleUser is null)
        {
            return ApplicationErrors.Identity.InvalidExternalToken;
        }

        if (!googleUser.EmailVerified)
        {
            return ApplicationErrors.Identity.ExternalEmailNotVerified;
        }

        ApplicationUser? user = await _identityService.FindByLoginAsync(
            LoginProvider,
            googleUser.ProviderKey);

        if (user is null)
        {
            user = await _identityService.FindByEmailAsync(googleUser.Email);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    Email = googleUser.Email,
                    UserName = googleUser.Email,
                    FirstName = googleUser.FirstName ?? string.Empty,
                    LastName = googleUser.LastName ?? string.Empty,
                    EmailConfirmed = true
                };

                IdentityResultSummary createResult = await _identityService.CreateAsync(
                    user,
                    Guid.CreateVersion7().ToString("N") + "Aa1");

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
            }

            IdentityResultSummary loginResult = await _identityService.AddLoginAsync(
                user,
                LoginProvider,
                googleUser.ProviderKey,
                LoginProvider);

            if (!loginResult.Succeeded)
            {
                return loginResult.ToApplicationErrors();
            }
        }

        if (user.IsDisabled)
        {
            return ApplicationErrors.Identity.UserDisabled;
        }

        if (await _identityService.IsLockedOutAsync(user))
        {
            return ApplicationErrors.Identity.UserLockedOut;
        }

        IReadOnlyCollection<string> roles = await _identityService.GetRolesAsync(user);
        TokenPair tokenPair = await _tokenProvider.CreateTokenPairAsync(
            user,
            cancellationToken);

        return user.ToAuthResponse(roles, tokenPair);
    }
}
