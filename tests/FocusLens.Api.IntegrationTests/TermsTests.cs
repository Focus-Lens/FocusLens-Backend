using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace FocusLens.Api.IntegrationTests;

public class TermsTests
{
    [Fact]
    public async Task GetTerms_ReturnsCurrentTerms()
    {
        await using CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        JsonElement terms = await client.GetFromJsonAsync<JsonElement>("/api/terms");

        Assert.Equal("2026-09-04", terms.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(terms.GetProperty("content").GetString()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SubmitDecision_ReturnsDecision(bool accepted)
    {
        await using CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/terms/decision",
            new { accepted });
        JsonElement decision = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(accepted, decision.GetProperty("accepted").GetBoolean());
        Assert.False(decision.TryGetProperty("accessToken", out _));
        Assert.False(decision.TryGetProperty("refreshToken", out _));
        Assert.False(decision.TryGetProperty("registrationToken", out _));
    }

    [Fact]
    public async Task SubmitDecision_WhenAcceptedIsMissing_ReturnsBadRequest()
    {
        await using CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using StringContent content = new("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/terms/decision", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}