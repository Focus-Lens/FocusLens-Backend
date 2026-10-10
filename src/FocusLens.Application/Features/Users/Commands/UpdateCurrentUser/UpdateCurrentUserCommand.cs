using FocusLens.Application.Features.Users.Dtos;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Users.Commands.UpdateCurrentUser;

public sealed record UpdateCurrentUserCommand(
    string FirstName,
    string LastName,
    string? PhoneNumber) : IRequest<Result<UserProfileDto>>;