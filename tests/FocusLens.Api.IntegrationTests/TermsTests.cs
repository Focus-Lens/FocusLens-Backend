using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Identity;
using FocusLens.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Api.IntegrationTests;

public class TermsTests
{
    [Theory]
    [InlineData("Parent")]
    [InlineData("Student")]
    public async Task GetTerms_ReturnsCurrentPublishedTermsForAudience(string audience)
    {
        await using var factory = new CustomWebApplicationFactory();
        LegalDocument terms = CreatePublishedTerms(
            audience,
            "2026-09-04",
            $"{audience} terms",
            new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));

        await factory.SeedAsync(db =>
        {
            db.LegalDocuments.Add(terms);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();

        JsonElement response = await client.GetFromJsonAsync<JsonElement>(
            $"/api/terms?audience={audience}");

        Assert.Equal(terms.Id, response.GetProperty("id").GetGuid());
        Assert.Equal(audience, response.GetProperty("audience").GetString());
        Assert.Equal("2026-09-04", response.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(response.GetProperty("content").GetString()));
    }

    [Fact]
    public async Task GetTerms_WhenMultipleVersionsArePublished_ReturnsLatestAndKeepsHistory()
    {
        await using var factory = new CustomWebApplicationFactory();
        LegalDocument oldTerms = CreatePublishedTerms(
            "Student",
            "2026-09-04",
            "old terms",
            new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));
        LegalDocument newTerms = CreatePublishedTerms(
            "Student",
            "2026-10-01",
            "new terms",
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

        await factory.SeedAsync(db =>
        {
            db.LegalDocuments.AddRange(oldTerms, newTerms);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();

        JsonElement response = await client.GetFromJsonAsync<JsonElement>(
            "/api/terms?audience=Student");

        Assert.Equal(newTerms.Id, response.GetProperty("id").GetGuid());
        Assert.Equal("2026-10-01", response.GetProperty("version").GetString());

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        Assert.Equal(2, dbContext.LegalDocuments.Count());
    }

    [Fact]
    public async Task SubmitDecision_WhenAccepted_RecordsExactLegalVersion()
    {
        await using var factory = new CustomWebApplicationFactory();
        var user = new ApplicationUser
        {
            Email = "student@example.com",
            UserName = "student@example.com",
            FirstName = "Focus",
            LastName = "Student"
        };
        LegalDocument terms = CreatePublishedTerms(
            "Student",
            "2026-09-04",
            "Student terms",
            new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));

        await factory.SeedAsync(db =>
        {
            db.Users.Add(user);
            db.LegalDocuments.Add(terms);
            return Task.CompletedTask;
        });

        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create(user.Id, ApplicationRoles.Student));

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/terms/decision",
            new { termsId = terms.Id, accepted = true });
        JsonElement decision = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(decision.GetProperty("accepted").GetBoolean());
        Assert.Equal(terms.Id, decision.GetProperty("termsId").GetGuid());
        Assert.Equal("Student", decision.GetProperty("audience").GetString());
        Assert.Equal("2026-09-04", decision.GetProperty("version").GetString());
        Assert.False(decision.TryGetProperty("accessToken", out _));
        Assert.False(decision.TryGetProperty("refreshToken", out _));
        Assert.False(decision.TryGetProperty("registrationToken", out _));

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        UserTermsAcceptance acceptance = Assert.Single(dbContext.UserTermsAcceptances);
        Assert.Equal(user.Id, acceptance.UserId);
        Assert.Equal(terms.Id, acceptance.LegalDocumentId);
    }

    [Fact]
    public async Task SubmitDecision_WhenRejected_ReturnsDecisionWithoutRecordingAcceptance()
    {
        await using var factory = new CustomWebApplicationFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/terms/decision",
            new { accepted = false });
        JsonElement decision = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(decision.GetProperty("accepted").GetBoolean());
    }

    [Fact]
    public async Task SubmitDecision_WhenAcceptedIsMissing_ReturnsBadRequest()
    {
        await using var factory = new CustomWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        using StringContent content = new("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/terms/decision", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static LegalDocument CreatePublishedTerms(
        string audience,
        string version,
        string content,
        DateTimeOffset publishedOnUtc)
        => new(
            Enum.Parse<LegalDocumentAudience>(audience),
            version,
            content,
            isPublished: true,
            publishedOnUtc);
}
