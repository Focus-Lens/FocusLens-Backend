using System.Net;
using System.Net.Http.Headers;

namespace FocusLens.Api.IntegrationTests;

public class AuthorizationTests
{
    [Fact]
    public async Task GetParentMe_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/parents/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetParentMe_WithStudentToken_ReturnsForbidden()
    {
        await using var factory = new CustomWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenFactory.Create(Guid.NewGuid(), "Student"));

        HttpResponseMessage response = await client.GetAsync("/api/parents/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetStudentMe_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/students/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStudentMe_WithParentToken_ReturnsForbidden()
    {
        await using var factory = new CustomWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenFactory.Create(Guid.NewGuid(), "Parent"));

        HttpResponseMessage response = await client.GetAsync("/api/students/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvitation_WithStudentToken_ReturnsForbidden()
    {
        await using var factory = new CustomWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenFactory.Create(Guid.NewGuid(), "Student"));

        using StringContent content = new(
            """{"studentEmail":"student@example.com"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync(
            "/api/access/invitations",
            content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_WithParentToken_ReturnsForbidden()
    {
        await using var factory = new CustomWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenFactory.Create(Guid.NewGuid(), "Parent"));

        HttpResponseMessage response = await client.PostAsync(
            $"/api/access/invitations/{Guid.NewGuid()}/accept",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
