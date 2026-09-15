using FocusLens.Contracts.BehavioralIntelligence;

namespace FocusLens.Application.Common.Interfaces;

public interface IBehavioralIntelligenceClient
{
    Task<BehaviorWindowResponse> AnalyzeWindowAsync(
        BehaviorWindowRequest request,
        CancellationToken cancellationToken);
}
