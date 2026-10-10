using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Students;

public sealed record DeleteStudyHistoryCommand : IRequest<Result<Success>>;