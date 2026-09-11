using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.RegisterStudent;

public sealed record RegisterStudentCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    Guid? TermsId,
    bool AcceptTerms) : IRequest<Result<Success>>;
