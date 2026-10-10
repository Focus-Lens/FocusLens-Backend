using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.Common.Interfaces;

public interface IStudySessionFinalBehaviorAnalysisService
{
    Task<Result<Success>> AnalyzeFinalAsync(
        Guid sessionId,
        CancellationToken cancellationToken);
}