using System.Net;
using System.Net.Http.Json;

using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Identity;
using FocusLens.Infrastructure.Data;
using FocusLens.Infrastructure.Identity.Verification;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FocusLens.Api.IntegrationTests;

public class AuthRegistrationTests
{
    [Fact]
    public async Task IdentityPasswordPolicy_MatchesWebRequirements()
    {
        await using var factory = new CustomWebApplicationFactory();

        IdentityOptions options = factory.Services
            .GetRequiredService<IOptions<IdentityOptions>>()
            .Value;

        Assert.Equal(8, options.Password.RequiredLength);
        Assert.True(options.Password.RequireDigit);
        Assert.True(options.Password.RequireUppercase);
        Assert.True(options.Password.RequireLowercase);
        Assert.True(options.Password.RequireNonAlphanumeric);
    }

    [Fact]
    public async Task RegisterStudent_ProvisionsStudentRoleEntityTermsAcceptanceAndTenMinuteCode()
    {
        await using var factory = new CustomWebApplicationFactory();
        Guid termsId = await SeedRegistrationDependenciesAsync(
            factory,
            LegalDocumentAudience.Student);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register/student",
            new
            {
                email = "student@example.com",
                password = "Password1!",
                firstName = "Focus",
                lastName = "Student",
                termsId,
                acceptTerms = true
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        ApplicationUser user = Assert.Single(dbContext.Users);
        Assert.Contains(dbContext.UserRoles, userRole => userRole.UserId == user.Id);
        Assert.Single(dbContext.Students);
        Assert.Empty(dbContext.Parents);

        UserTermsAcceptance acceptance = Assert.Single(dbContext.UserTermsAcceptances);
        Assert.Equal(user.Id, acceptance.UserId);
        Assert.Equal(termsId, acceptance.LegalDocumentId);

        EmailVerificationCode code = Assert.Single(dbContext.EmailVerificationCodes);
        TimeSpan codeLifetime = code.ExpiresOnUtc - code.CreatedOnUtc;
        Assert.InRange(
            codeLifetime,
            TimeSpan.FromMinutes(9).Add(TimeSpan.FromSeconds(30)),
            TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task RegisterParent_ProvisionsParentRoleAndEntity()
    {
        await using var factory = new CustomWebApplicationFactory();
        Guid termsId = await SeedRegistrationDependenciesAsync(
            factory,
            LegalDocumentAudience.Parent);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register/parent",
            new
            {
                email = "parent@example.com",
                password = "Password1!",
                firstName = "Focus",
                lastName = "Parent",
                termsId,
                acceptTerms = true
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        ApplicationUser user = Assert.Single(dbContext.Users);
        ApplicationRole parentRole = Assert.Single(
            dbContext.Roles,
            role => role.Name == ApplicationRoles.Parent);

        Assert.Contains(
            dbContext.UserRoles,
            userRole => userRole.UserId == user.Id
                && userRole.RoleId == parentRole.Id);
        Assert.Single(dbContext.Parents);
        Assert.Empty(dbContext.Students);
    }

    [Fact]
    public async Task RegisterParent_WhenPasswordHasNoSymbol_ReturnsBadRequest()
    {
        await using var factory = new CustomWebApplicationFactory();
        Guid termsId = await SeedRegistrationDependenciesAsync(
            factory,
            LegalDocumentAudience.Parent);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register/parent",
            new
            {
                email = "weak-parent@example.com",
                password = "Password1",
                firstName = "Focus",
                lastName = "Parent",
                termsId,
                acceptTerms = true
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Guid> SeedRegistrationDependenciesAsync(
        CustomWebApplicationFactory factory,
        LegalDocumentAudience audience)
    {
        LegalDocument terms = new(
            audience,
            "2026-09-04",
            $"{audience} terms",
            isPublished: true,
            new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));

        await factory.SeedAsync(db =>
        {
            db.Roles.AddRange(
                CreateRole(ApplicationRoles.Student),
                CreateRole(ApplicationRoles.Parent));
            db.LegalDocuments.Add(terms);
            return Task.CompletedTask;
        });

        return terms.Id;
    }

    private static ApplicationRole CreateRole(string name)
        => new()
        {
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            IsDefault = true
        };
}
