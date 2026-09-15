using FocusLens.Application.Features.Users.Dtos;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Users.Queries.GetCurrentUserAccountStatus;

public sealed record GetCurrentUserAccountStatusQuery
    : IRequest<Result<UserAccountStatusDto>>;
