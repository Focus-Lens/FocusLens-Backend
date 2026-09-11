using FocusLens.Domain;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.GoogleLogin;

public sealed record GoogleLoginCommand(
    string IdToken,
    LegalDocumentAudience AccountType) : IRequest<Result<AuthResponse>>;
