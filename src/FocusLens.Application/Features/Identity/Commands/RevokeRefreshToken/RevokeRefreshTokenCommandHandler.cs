using FocusLens.Domain.Common.Interfaces;
using FocusLens.Application.Common.Errors;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.RevokeRefreshToken;

public sealed class RevokeRefreshTokenCommandHandler
    : IRequestHandler<RevokeRefreshTokenCommand, Result<Success>>
{
    private readonly ITokenProvider _tokenProvider;

    public RevokeRefreshTokenCommandHandler(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public async Task<Result<Success>> Handle(
        RevokeRefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        bool revoked = await _tokenProvider.RevokeRefreshTokenAsync(
            request.RefreshToken,
            cancellationToken);

        return revoked
            ? Result.Success
            : ApplicationErrors.Identity.InvalidRefreshToken;
    }
}
