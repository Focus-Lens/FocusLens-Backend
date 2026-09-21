using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.CheckEmail;

public sealed record CheckEmailCommand(
    string Email) : IRequest<bool>;
