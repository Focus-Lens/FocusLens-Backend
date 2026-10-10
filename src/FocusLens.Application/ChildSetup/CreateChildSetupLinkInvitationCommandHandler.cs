using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class CreateChildSetupLinkInvitationCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupDraft> draftRepository,
    IBaseRepository<ChildSetupInvitation> invitationRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInvitationUrlBuilder invitationUrlBuilder,
    IChildSetupInvitationTokenProtector tokenProtector,
    TimeProvider timeProvider
) : IRequestHandler<CreateChildSetupLinkInvitationCommand, Result<ChildSetupInvitationResponse>>
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public async Task<Result<ChildSetupInvitationResponse>> Handle(
        CreateChildSetupLinkInvitationCommand request,
        CancellationToken cancellationToken
    )
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Parents.CurrentUserUnavailable",
                "The current user could not be identified."
            );
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(item => item.UserId == userId);

        if (parent is null)
        {
            return Error.NotFound(
                "Parents.NotFound",
                "The current user does not have a parent profile."
            );
        }

        if (request.DraftId == Guid.Empty)
        {
            return Error.Validation(
                "ChildSetup.InvalidDraftId",
                "The child setup draft ID is invalid."
            );
        }

        ChildSetupDraft? draft = await draftRepository.GetByIdAsync(
            request.DraftId,
            item => item.Subjects
        );

        if (draft is null || draft.ParentId != parent.Id)
        {
            return Error.NotFound(
                "ChildSetup.NotFound",
                "The child setup draft could not be found."
            );
        }

        if (draft.Status != ChildSetupStatus.Draft)
        {
            return Error.Conflict(
                "ChildSetup.DraftAlreadyInvited",
                "This child setup has already been invited."
            );
        }

        if (draft.ProfileSetupMode is null)
        {
            return Error.Validation(
                "ChildSetup.ProfileSetupModeRequired",
                "Choose how the child profile will be set up before sending an invitation."
            );
        }

        if (draft.ProfileSetupMode == ChildSetupProfileMode.ParentManaged
            && !draft.IsProfileComplete())
        {
            return Error.Validation(
                "ChildSetup.Incomplete",
                "Complete the child setup before sending an invitation."
            );
        }

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        string protectedToken = tokenProtector.Protect(token);
        DateTimeOffset expiresAtUtc = timeProvider.GetUtcNow().Add(InvitationLifetime);

        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Link,
            null,
            tokenHash,
            expiresAtUtc,
            protectedToken
        );

        invitationRepository.Add(invitation);
        draft.MarkInvited();

        await unitOfWork.SaveChangesAsync();

        return new ChildSetupInvitationResponse(
            invitation.Id,
            invitation.Status.ToString(),
            invitation.ExpiresAtUtc,
            invitationUrlBuilder.CreateChildSetupInvitationUrl(token)
        );
    }
}