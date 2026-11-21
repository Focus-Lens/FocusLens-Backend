using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class GetMyParentQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<FocusLens.Domain.Access.ParentStudentRelationship> relationshipRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyParentQuery, ParentResponse?>
{
    public async Task<ParentResponse?> Handle(
        GetMyParentQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            parent => parent.UserId == userId,
            parent => parent.User);

        if (parent is null)
        {
            return null;
        }

        bool selectionChanged = await ActiveChildSelection.ResolveAsync(parent, relationshipRepository);
        if (selectionChanged)
        {
            await unitOfWork.SaveChangesAsync();
        }

        return new ParentResponse(
            parent.Id,
            parent.UserId,
            parent.User.FirstName,
            parent.User.LastName,
            parent.WeekStartsOn,
            parent.WeekStartsOn is null,
            parent.ActiveChildId);
    }
}
