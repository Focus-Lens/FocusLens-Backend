using System.Net;
using System.Text;
using FocusLens.Contracts.BehavioralIntelligence;
using FocusLens.Infrastructure.AI;

namespace FocusLens.Api.IntegrationTests;

public sealed class BehavioralIntelligenceClientTests
{
    [Fact]
    public async Task AnalyzeWindowAsync_MapsMixedCaseSectionFieldsFromAi1Json()
    {
        const string responseJson = """
        {
          "session_id": "session-001",
          "window_index": 1,
          "window_focus_score": 87,
          "window_active_time_seconds": 240.0,
          "window_understanding_score": null,
          "understanding_trend": "STABLE",
          "window_state": "NORMAL_FOCUSED",
          "recommended_action": "CONTINUE",
          "raw_action": "CONTINUE",
          "action_emitted": true,
          "trend": "STABLE",
          "is_final": false,
          "sections_analyzed": 1,
          "sections": [
            {
              "section_id": "section-001",
              "concept_id": "concept-001",
              "state": "NORMAL_FOCUSED",
              "confidence": 0.75,
              "rule_match_score": 0.75,
              "focusScore": 73,
              "recommendedAction": "SLOW_PACE",
              "features_used": { "scroll_speed": "FAST" },
              "mcq_data_available": false
            }
          ]
        }
        """;

        using HttpClient httpClient = new(new StubHandler(responseJson))
        {
            BaseAddress = new Uri("http://ai1.test/")
        };
        BehavioralIntelligenceClient client = new(httpClient);
        BehaviorWindowRequest request = new(
            "user-001", "session-001", 1, 1_694_123_300_000, 1_694_123_600_000,
            false, Array.Empty<BehaviorSection>(), Array.Empty<BehaviorWindowHistoryItem>());

        BehaviorWindowResponse response = await client.AnalyzeWindowAsync(request, CancellationToken.None);
        BehaviorSectionResult section = Assert.Single(response.Sections);

        Assert.Equal(87, response.WindowFocusScore);
        Assert.Equal(240d, response.WindowActiveTimeSeconds!.Value);
        Assert.Equal(73, section.FocusScore);
        Assert.Equal("SLOW_PACE", section.RecommendedAction);
    }

    private sealed class StubHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/ai2/analyze-window", request.RequestUri?.AbsolutePath);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
