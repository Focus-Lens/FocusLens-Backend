using System.Net;

namespace FocusLens.Api.IntegrationTests;

public class HealthTests
{
    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        await using CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}