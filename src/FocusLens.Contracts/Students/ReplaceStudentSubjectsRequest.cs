namespace FocusLens.Contracts.Students;

public sealed record ReplaceStudentSubjectsRequest(IReadOnlyCollection<Guid> SubjectIds);
