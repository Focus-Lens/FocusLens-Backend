using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class UpdateParentSettingsCommandHandler(
    IBaseRepository<Parent> parentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateParentSettingsCommand, Result<ParentSettingsResponse>>
{
    public async Task<Result<ParentSettingsResponse>> Handle(
        UpdateParentSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Parents.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);

        if (parent is null)
        {
            return Error.NotFound(
                "Parents.NotFound",
                "The current user does not have a parent profile.");
        }

        if (!Enum.IsDefined(request.Request.WeekStartsOn))
        {
            return Error.Validation(
                "Parents.InvalidWeekStartsOn",
                "WeekStartsOn must be a valid day of week.");
        }

        parent.SetWeekStartsOn(request.Request.WeekStartsOn);

        await unitOfWork.SaveChangesAsync();

        return new ParentSettingsResponse(
            parent.WeekStartsOn,
            parent.WeekStartsOn is null);
    }
}