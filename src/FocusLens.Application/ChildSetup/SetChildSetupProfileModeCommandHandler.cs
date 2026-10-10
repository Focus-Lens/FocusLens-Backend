using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class SetChildSetupProfileModeCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupDraft> childSetupDraftRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork
) : IRequestHandler<SetChildSetupProfileModeCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        SetChildSetupProfileModeCommand request,
        CancellationToken cancellationToken
    )
    {
        if (currentUser.UserId is not { } userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Parents.CurrentUserUnavailable",
                "The current user could not be identified."
            );
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(item => item.UserId == userId
        );

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

        ChildSetupDraft? draft = await childSetupDraftRepository.GetByIdAsync(
            request.DraftId
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
            return Error.Validation(
                "ChildSetup.DraftNotEditable",
                "Only a draft child setup can be edited."
            );
        }

        if (!Enum.TryParse(
                request.ProfileSetupMode?.Trim(),
                true,
                out ChildSetupProfileMode mode)
            || !Enum.IsDefined(mode))
        {
            return Error.Validation(
                "ChildSetup.InvalidProfileSetupMode",
                "Profile setup mode is invalid."
            );
        }

        draft.SetProfileSetupMode(mode);

        await unitOfWork.SaveChangesAsync();

        return true;
    }
}