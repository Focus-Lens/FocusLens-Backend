using MediatR;

namespace FocusLens.Application.Students;

public sealed record ExportStudentDataQuery
    : IRequest<StudentDataExport>;

public sealed record StudentDataExport(
    string FileName,
    string ContentType,
    byte[] Content);
