using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FocusLens.Domain;
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
