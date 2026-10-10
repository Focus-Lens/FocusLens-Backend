using System.Net.Http.Json;
using System.Text.Json;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.BehavioralIntelligence;

namespace FocusLens.Infrastructure.AI;

public sealed class BehavioralIntelligenceClient(HttpClient httpClient) : IBehavioralIntelligenceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<BehaviorWindowResponse> AnalyzeWindowAsync(
        BehaviorWindowRequest request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "ai2/analyze-window",
            request,
            JsonOptions,
            cancellationToken);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Behavioral Intelligence request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body}");
        }

        BehaviorWindowResponse? result = JsonSerializer.Deserialize<BehaviorWindowResponse>(body, JsonOptions);

        return result
               ?? throw new InvalidOperationException(
                   "Behavioral Intelligence returned an empty or invalid response.");
    }
}