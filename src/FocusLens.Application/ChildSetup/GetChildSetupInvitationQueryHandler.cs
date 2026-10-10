using System.Security.Cryptography;
using System.Text;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed class GetChildSetupInvitationQueryHandler(
    IBaseRepository<ChildSetupInvitation> invitationRepository,
    TimeProvider timeProvider
) : IRequestHandler<GetChildSetupInvitationQuery, Result<ChildSetupInvitationDetailsResponse>>
{
    public async Task<Result<ChildSetupInvitationDetailsResponse>> Handle(
        GetChildSetupInvitationQuery request,
        CancellationToken cancellationToken
    )
    {
        string token = request.Token?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return Error.NotFound(
                "ChildSetupInvitation.NotFound",
                "The invitation could not be found."
            );
        }

        string tokenHash = HashToken(token);

        ChildSetupInvitation? invitation = await invitationRepository.FirstOrDefaultAsync(item =>
            item.TokenHash == tokenHash
        );

        if (invitation is null)
        {
            return Error.NotFound(
                "ChildSetupInvitation.NotFound",
                "The invitation could not be found."
            );
        }

        if (invitation.Status != ChildSetupInvitationStatus.Pending)
        {
            return Error.Conflict(
                "ChildSetupInvitation.Unavailable",
                "This invitation is no longer available."
            );
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (invitation.IsExpired(now))
        {
            return Error.Conflict("ChildSetupInvitation.Expired", "This invitation has expired.");
        }

        return new ChildSetupInvitationDetailsResponse(
            invitation.Status.ToString(),
            invitation.Type.ToString(),
            invitation.TargetEmailNormalized,
            invitation.ExpiresAtUtc
        );
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}