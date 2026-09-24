using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class UpdateChildSetupDraftProfileImageCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupDraft> childSetupDraftRepository,
    IProfileImageFileStore fileStore,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateChildSetupDraftProfileImageCommand, Result<string>>
{
    private static readonly HashSet<string> AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    public async Task<Result<string>> Handle(
        UpdateChildSetupDraftProfileImageCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Parents.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId);

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

        ChildSetupDraft? draft =
            await childSetupDraftRepository.GetByIdAsync(request.DraftId);

        if (draft is null || draft.ParentId != parent.Id)
        {
            return Error.NotFound(
                "ChildSetup.NotFound",
                "The child setup draft could not be found.");
        }

        if (draft.Status != ChildSetupStatus.Draft)
        {
            return Error.Validation(
                "ChildSetup.DraftNotEditable",
                "Only a draft child setup can be edited.");
        }

        string extension = Path.GetExtension(request.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            return Error.Validation(
                "ChildSetup.InvalidProfileImage",
                "Profile image must be a JPG, PNG, or WebP image.");
        }

        if (request.FileSizeBytes <= 0 ||
            request.FileSizeBytes > 10 * 1024 * 1024)
        {
            return Error.Validation(
                "ChildSetup.ProfileImageSizeInvalid",
                "Profile image must be between 1 byte and 10 MB.");
        }

        string? previousReference = draft.ProfileImageStorageReference;

        await using Stream content =
            await request.OpenReadAsync(cancellationToken);

        string reference = await fileStore.SaveAsync(
            draft.Id,
            extension,
            content,
            cancellationToken);

        draft.SetProfileImageStorageReference(reference);

        childSetupDraftRepository.Update(draft);
        await unitOfWork.SaveChangesAsync();

        if (previousReference is not null)
        {
            await fileStore.DeleteAsync(
                previousReference,
                cancellationToken);
        }

        return reference;
    }
}
