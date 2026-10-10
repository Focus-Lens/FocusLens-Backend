using FocusLens.Contracts;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Parents;

public sealed record GetParentDashboardQuery(Guid StudentId)
    : IRequest<Result<ParentDashboardResponse>>;

public sealed record GetParentDashboardSessionsQuery(
    Guid StudentId,
    IReadOnlyCollection<string>? Statuses,
    Guid? SubjectId,
    int Page = 1,
    int PageSize = 20)
    : IRequest<Result<ParentDashboardSessionHistoryResponse>>;

public sealed record ExportParentDashboardSessionsQuery(
    Guid StudentId,
    IReadOnlyCollection<string>? Statuses,
    Guid? SubjectId)
    : IRequest<Result<ParentDashboardSessionsCsvExport>>;

public sealed record ParentDashboardSessionsCsvExport(
    string FileName,
    string ContentType,
    byte[] Content);