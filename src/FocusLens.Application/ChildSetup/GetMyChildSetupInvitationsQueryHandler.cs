using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class GetMyChildSetupInvitationsQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupInvitation> invitationRepository,
    ICurrentUser currentUser)
    : IRequestHandler<
        GetMyChildSetupInvitationsQuery,
        IReadOnlyList<ParentChildSetupInvitationResponse>>
{
    public async Task<IReadOnlyList<ParentChildSetupInvitationResponse>> Handle(
        GetMyChildSetupInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return [];
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);

        if (parent is null)
        {
            return [];
        }

        IEnumerable<ChildSetupInvitation> invitations =
            await invitationRepository.GetAllAsync(
                invitation =>
                    invitation.Status == ChildSetupInvitationStatus.Pending &&
                    invitation.ChildSetupDraft.ParentId == parent.Id,
                invitation => invitation.ChildSetupDraft);

        return invitations
            .OrderBy(invitation => invitation.ExpiresAtUtc)
            .Select(invitation => new ParentChildSetupInvitationResponse(
                invitation.ChildSetupDraftId,
                invitation.Id,
                invitation.ChildSetupDraft.FirstName,
                invitation.ChildSetupDraft.LastName,
                invitation.TargetEmailNormalized,
                invitation.Status.ToString(),
                invitation.ExpiresAtUtc,
                invitation.ChildSetupDraft.Grade is null
                    ? null
                    : StudentEnumMapper.ToContract(
                        invitation.ChildSetupDraft.Grade.Value)))
            .ToList();
    }
}
