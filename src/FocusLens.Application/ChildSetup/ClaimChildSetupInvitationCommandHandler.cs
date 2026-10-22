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
            return InvitationNotFound();
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
            return InvitationNotFound();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (invitation.IsExpired(now))
        {
            return InvitationUnavailable();
        }

        ChildSetupDraft? draft = await draftRepository.GetByIdAsync(invitation.ChildSetupDraftId);

        if (draft is null)
        {
            return InvitationUnavailable();
        }

        // A retry after a successful claim must be safe. Ownership is already
        // bound to this student, so a repeated request must not claim again or
        // require a second persistence operation.
        if (
            invitation.Status == ChildSetupInvitationStatus.Claimed
            && draft.Status == ChildSetupStatus.Claimed
            && draft.ClaimedByStudentId == student.Id
        )
        {
            return new ClaimChildSetupInvitationResponse(
                draft.Id,
                draft.Status.ToString(),
                invitation.Status.ToString()
            );
        }

        if (invitation.Status != ChildSetupInvitationStatus.Pending)
        {
            return InvitationUnavailable();
        }

        if (draft.Status != ChildSetupStatus.Invited)
        {
            return InvitationUnavailable();
        }

        // Verify the invited address on the initial claim. Never bypass this
        // check for a pending Email invitation; Link invitations have no target email.
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

    private static Error InvitationNotFound() =>
        Error.NotFound("ChildSetupInvitation.NotFound", "The invitation could not be found.");

    private static Error InvitationUnavailable() => InvitationNotFound();
}
