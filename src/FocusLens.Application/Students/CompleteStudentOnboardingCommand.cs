using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Contracts.Students;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Students;

public sealed record CompleteStudentOnboardingCommand(
    CompleteStudentOnboardingRequest Request
) : IRequest<Result<AuthResponse>>;