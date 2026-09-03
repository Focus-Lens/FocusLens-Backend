using System.Net;

namespace FocusLens.Api.IntegrationTests;

public class HealthTests
{
    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        await using var factory = new CustomWebApplicationFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
