using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Contracts.Parents;
using FocusLens.Domain;
using FocusLens.Application.Common.Interfaces;
using MediatR;

namespace FocusLens.Application.Parents;

public sealed class GetMyParentQueryHandler(
    IBaseRepository<Parent> parentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyParentQuery, ParentResponse?>
{
    public async Task<ParentResponse?> Handle(
        GetMyParentQuery request,
        CancellationToken cancellationToken)
    {
        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            parent => parent.UserId == currentUser.UserId);

        return parent is null
            ? null
            : new ParentResponse(parent.Id, parent.UserId);
    }
}
