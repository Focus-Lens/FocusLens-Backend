using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class GetChildSetupInvitationLinkQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupDraft> draftRepository,
    IBaseRepository<ChildSetupInvitation> invitationRepository,
    ICurrentUser currentUser,
    IInvitationUrlBuilder invitationUrlBuilder,
    IChildSetupInvitationTokenProtector tokenProtector,
    TimeProvider timeProvider)
    : IRequestHandler<GetChildSetupInvitationLinkQuery, Result<ChildSetupInvitationLinkResponse>>
{
    public async Task<Result<ChildSetupInvitationLinkResponse>> Handle(
        GetChildSetupInvitationLinkQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Parents.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(item => item.UserId == userId);

        if (parent is null)
        {
            return Error.NotFound(
                "Parents.NotFound",
                "The current user does not have a parent profile.");
        }

        if (request.DraftId == Guid.Empty)
        {
            return Error.Validation(
                "ChildSetup.InvalidDraftId",
                "The child setup draft ID is invalid.");
        }

        ChildSetupDraft? draft = await draftRepository.FirstOrDefaultAsync(item => item.Id == request.DraftId);

        if (draft is null || draft.ParentId != parent.Id)
        {
            return Error.NotFound(
                "ChildSetup.NotFound",
                "The child setup draft could not be found.");
        }

        if (draft.Status != ChildSetupStatus.Invited)
        {
            return Error.Conflict(
                "ChildSetup.NotInvited",
                "This child setup does not have an active invitation.");
        }

        ChildSetupInvitation? invitation = await invitationRepository.FirstOrDefaultAsync(item =>
            item.ChildSetupDraftId == draft.Id
            && item.Status == ChildSetupInvitationStatus.Pending);

        if (invitation is null)
        {
            return Error.NotFound(
                "ChildSetupInvitation.NotFound",
                "No pending invitation exists for this child setup.");
        }

        if (invitation.IsExpired(timeProvider.GetUtcNow()))
        {
            return Error.Conflict(
                "ChildSetupInvitation.Expired",
                "This invitation has expired.");
        }

        if (string.IsNullOrWhiteSpace(invitation.ProtectedToken))
        {
            return Error.Conflict(
                "ChildSetupInvitation.LinkUnavailable",
                "This invitation link cannot be copied. Resend the invitation first.");
        }

        string token;

        try
        {
            token = tokenProtector.Unprotect(invitation.ProtectedToken);
        }
        catch (InvalidOperationException)
        {
            return Error.Conflict(
                "ChildSetupInvitation.LinkUnavailable",
                "This invitation link cannot be copied. Resend the invitation first.");
        }

        return new ChildSetupInvitationLinkResponse(
            invitationUrlBuilder.CreateChildSetupInvitationUrl(token));
    }
}
