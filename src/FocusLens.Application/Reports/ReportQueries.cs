using FocusLens.Contracts.Reports;
using MediatR;

namespace FocusLens.Application.Reports;

public sealed record GetReportSessionsQuery(
    Guid? StudentId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    Guid? SubjectId,
    string? Status,
    int Page = 1,
    int PageSize = 20) : IRequest<ReportSessionListResponse?>;

public sealed record GetReportSessionDetailQuery(
    Guid SessionId,
    Guid? StudentId) : IRequest<ReportSessionDetailResponse?>;