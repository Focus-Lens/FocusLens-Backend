using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.CheckEmail;

public sealed class CheckEmailCommandHandler
    : IRequestHandler<CheckEmailCommand, bool>
{
    private readonly IIdentityService _identityService;

    public CheckEmailCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<bool> Handle(
        CheckEmailCommand request,
        CancellationToken cancellationToken)
    {
        string email = request.Email.Trim();

        return await _identityService.FindByEmailAsync(email) is null;
    }
}
