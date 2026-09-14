using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record AnalyzeStudySessionContentCommand(Guid SessionId)
    : IRequest<Result<StudySessionContentAnalysisResponse>>;

public sealed record SelectStudySessionAiSectionsCommand(
    Guid SessionId,
    SelectStudySessionAiSectionsRequest Request)
    : IRequest<Result<StudySessionResponse>>;
