using FocusLens.Domain.Common.Interfaces;
using System.Security.Claims;

using FocusLens.Application.Common.Errors;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<TokenResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenProvider _tokenProvider;

    public RefreshTokenCommandHandler(
        IIdentityService identityService,
        ITokenProvider tokenProvider)
    {
        _identityService = identityService;
        _tokenProvider = tokenProvider;
    }

    public async Task<Result<TokenResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        ClaimsPrincipal principal;

        try
        {
            principal = _tokenProvider.GetPrincipalFromExpiredToken(request.AccessToken);
        }
        catch
        {
            return ApplicationErrors.Identity.InvalidRefreshToken;
        }

        string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out Guid parsedUserId))
        {
            return ApplicationErrors.Identity.InvalidRefreshToken;
        }

        ApplicationUser? user = await _identityService.FindByIdAsync(parsedUserId);

        if (user is null || user.IsDisabled)
        {
            return ApplicationErrors.Identity.InvalidRefreshToken;
        }

        TokenPair? tokenPair = await _tokenProvider.RotateRefreshTokenAsync(
            user,
            request.RefreshToken,
            cancellationToken);

        return tokenPair is null
            ? ApplicationErrors.Identity.InvalidRefreshToken
            : tokenPair.ToResponse();
    }
}
