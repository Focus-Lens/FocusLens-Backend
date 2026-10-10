using System.Security.Cryptography;
using System.Text;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class ClaimChildSetupInvitationCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ChildSetupInvitation> invitationRepository,
    IBaseRepository<ChildSetupDraft> draftRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider
) : IRequestHandler<ClaimChildSetupInvitationCommand, Result<ClaimChildSetupInvitationResponse>>
{
    public async Task<Result<ClaimChildSetupInvitationResponse>> Handle(
        ClaimChildSetupInvitationCommand request,
        CancellationToken cancellationToken
    )
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "ChildSetup.Unauthorized",
                "The current student could not be identified."
            );
        }

        string token = request.Token?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return Error.NotFound(
                "ChildSetupInvitation.NotFound",
                "The invitation could not be found."
            );
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(item =>
            item.UserId == userId
        );

        if (student is null)
        {
            return Error.NotFound(
                "Students.NotFound",
                "The current user does not have a student profile."
            );
        }

        string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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

        if (
            invitation.Type == ChildSetupInvitationType.Email
            && (
                string.IsNullOrWhiteSpace(currentUser.Email)
                || !string.Equals(
                    invitation.TargetEmailNormalized,
                    currentUser.Email.Trim().ToUpperInvariant(),
                    StringComparison.Ordinal
                )
            )
        )
        {
            return InvitationUnavailable();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (invitation.IsExpired(now))
        {
            return InvitationUnavailable();
        }

        if (invitation.Status != ChildSetupInvitationStatus.Pending)
        {
            return InvitationUnavailable();
        }

        ChildSetupDraft? draft = await draftRepository.GetByIdAsync(invitation.ChildSetupDraftId);

        if (draft is null)
        {
            return InvitationUnavailable();
        }

        if (draft.Status != ChildSetupStatus.Invited)
        {
            return InvitationUnavailable();
        }

        draft.MarkClaimed(student.Id);
        invitation.Claim(now);

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyConflictException)
        {
            return InvitationUnavailable();
        }

        return new ClaimChildSetupInvitationResponse(
            draft.Id,
            draft.Status.ToString(),
            invitation.Status.ToString()
        );
    }

    private static Error InvitationUnavailable() =>
        Error.NotFound("ChildSetupInvitation.NotFound", "The invitation could not be found.");
}