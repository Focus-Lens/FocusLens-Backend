using FocusLens.Application.Common.Errors;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.Login;

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenProvider _tokenProvider;

    public LoginCommandHandler(
        IIdentityService identityService,
        ITokenProvider tokenProvider)
    {
        _identityService = identityService;
        _tokenProvider = tokenProvider;
    }

    public async Task<Result<AuthResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _identityService.FindByEmailAsync(
            request.Email.Trim());

        if (user is null
            || !await _identityService.CheckPasswordAsync(user, request.Password))
        {
            return ApplicationErrors.Identity.InvalidCredentials;
        }

        if (user.IsDisabled)
        {
            return ApplicationErrors.Identity.UserDisabled;
        }

        if (await _identityService.IsLockedOutAsync(user))
        {
            return ApplicationErrors.Identity.UserLockedOut;
        }

        if (!await _identityService.IsEmailConfirmedAsync(user))
        {
            return ApplicationErrors.Identity.EmailNotConfirmed;
        }

        IReadOnlyCollection<string> roles = await _identityService.GetRolesAsync(user);
        TokenPair tokenPair = await _tokenProvider.CreateTokenPairAsync(
            user,
            cancellationToken);

        return user.ToAuthResponse(roles, tokenPair);
    }
}