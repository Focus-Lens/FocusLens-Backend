using FocusLens.Contracts.ChildSetup;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class CreateChildSetupDraftCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupDraft> childSetupDraftRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateChildSetupDraftCommand, Result<ChildSetupDraftResponse>>
{
    public async Task<Result<ChildSetupDraftResponse>> Handle(
        CreateChildSetupDraftCommand request,
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

        ChildSetupDraft draft = new(parent.Id);

        childSetupDraftRepository.Add(draft);

        await unitOfWork.SaveChangesAsync();

        return new ChildSetupDraftResponse(
            draft.Id,
            draft.Status.ToString(),
            draft.FirstName,
            draft.LastName,
            draft.DateOfBirth,
            null,
            Array.Empty<ChildSetupSubjectResponse>(),
            Array.Empty<StudyPriority>(),
            null,
            draft.ProfileSetupMode?.ToString(),
            draft.ProfileImageStorageReference);
    }
}