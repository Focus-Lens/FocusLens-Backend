using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.RegisterParent;

public sealed record RegisterParentCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    Guid? TermsId,
    bool AcceptTerms) : IRequest<Result<Success>>;
