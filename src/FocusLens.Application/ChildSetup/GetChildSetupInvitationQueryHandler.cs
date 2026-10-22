using System.Security.Cryptography;
using System.Text;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class GetChildSetupInvitationQueryHandler(
    IBaseRepository<ChildSetupInvitation> invitationRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ChildSetupDraft> draftRepository,
    ICurrentUser currentUser,
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
            return InvitationNotFound();
        }

        string tokenHash = HashToken(token);

        ChildSetupInvitation? invitation = await invitationRepository.FirstOrDefaultAsync(item =>
            item.TokenHash == tokenHash
        );

        if (invitation is null)
        {
            return InvitationNotFound();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (invitation.Status == ChildSetupInvitationStatus.Pending)
        {
            if (invitation.IsExpired(now))
            {
                return Error.Conflict(
                    "ChildSetupInvitation.Expired",
                    "This invitation has expired."
                );
            }

            return ToResponse(invitation);
        }

        // Once claimed, keep the review link usable only by the student who
        // claimed it. The initial invitation preview remains available anonymously
        // while Pending, but a Claimed token is not enough to read another student's setup.
        if (invitation.Status != ChildSetupInvitationStatus.Claimed
            || currentUser.UserId is not Guid userId
            || userId == Guid.Empty)
        {
            return InvitationUnavailable();
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(item =>
            item.UserId == userId
        );

        if (student is null)
        {
            return InvitationUnavailable();
        }

        ChildSetupDraft? draft = await draftRepository.GetByIdAsync(invitation.ChildSetupDraftId);

        if (
            draft is null
            || draft.Status != ChildSetupStatus.Claimed
            || draft.ClaimedByStudentId != student.Id
        )
        {
            return InvitationUnavailable();
        }

        return ToResponse(invitation);
    }

    private static ChildSetupInvitationDetailsResponse ToResponse(ChildSetupInvitation invitation) =>
        new(
            invitation.Status.ToString(),
            invitation.Type.ToString(),
            invitation.TargetEmailNormalized,
            invitation.ExpiresAtUtc
        );

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static Error InvitationNotFound() =>
        Error.NotFound("ChildSetupInvitation.NotFound", "The invitation could not be found.");

    private static Error InvitationUnavailable() =>
        Error.Conflict(
            "ChildSetupInvitation.Unavailable",
            "This invitation is no longer available."
        );
}
