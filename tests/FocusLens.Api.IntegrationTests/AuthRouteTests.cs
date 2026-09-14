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
    public async Task ContinueWithGoogleRoute_IsReplaced()
    {
        await using CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using StringContent content = new("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/continue-with-google",
            content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/google/student")]
    [InlineData("/api/auth/google/parent")]
    public async Task AccountTypedGoogleRoutes_Exist(string route)
    {
        await using CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using StringContent content = new("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(route, content);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}