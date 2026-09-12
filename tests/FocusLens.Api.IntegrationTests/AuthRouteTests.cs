using System.Net;
using System.Text;

namespace FocusLens.Api.IntegrationTests;

public class AuthRouteTests
{
    [Fact]
    public async Task GoogleLoginRoute_IsReplaced()
    {
        await using CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using StringContent content = new("""{"idToken":"token"}""", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/auth/google-login", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ContinueWithGoogleRoute_Exists()
    {
        await using CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using StringContent content = new("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/continue-with-google",
            content);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}