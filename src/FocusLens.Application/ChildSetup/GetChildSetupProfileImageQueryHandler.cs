using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class GetChildSetupProfileImageQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupDraft> childSetupDraftRepository,
    IProfileImageFileStore fileStore,
    ICurrentUser currentUser)
    : IRequestHandler<GetChildSetupProfileImageQuery, Result<ChildSetupProfileImageFile>>
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp"
    };

    public async Task<Result<ChildSetupProfileImageFile>> Handle(
        GetChildSetupProfileImageQuery request,
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

        ChildSetupDraft? draft = await childSetupDraftRepository.GetByIdAsync(request.DraftId);

        if (draft is null || draft.ParentId != parent.Id)
        {
            return Error.NotFound(
                "ChildSetup.NotFound",
                "The child setup draft could not be found.");
        }

        if (string.IsNullOrWhiteSpace(draft.ProfileImageStorageReference))
        {
            return Error.NotFound(
                "ChildSetup.ProfileImageNotFound",
                "The child setup profile image could not be found.");
        }

        string extension = Path.GetExtension(draft.ProfileImageStorageReference);

        if (!ContentTypes.TryGetValue(extension, out string? contentType))
        {
            return Error.NotFound(
                "ChildSetup.ProfileImageNotFound",
                "The child setup profile image could not be found.");
        }

        try
        {
            Stream content = await fileStore.OpenReadAsync(
                draft.ProfileImageStorageReference,
                cancellationToken);

            return new ChildSetupProfileImageFile(content, contentType);
        }
        catch (FileNotFoundException)
        {
            return Error.NotFound(
                "ChildSetup.ProfileImageNotFound",
                "The child setup profile image could not be found.");
        }
        catch (DirectoryNotFoundException)
        {
            return Error.NotFound(
                "ChildSetup.ProfileImageNotFound",
                "The child setup profile image could not be found.");
        }
    }
}