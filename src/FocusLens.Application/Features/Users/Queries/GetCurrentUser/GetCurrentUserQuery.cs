using FocusLens.Application.Features.Users.Dtos;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Users.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<Result<UserProfileDto>>;