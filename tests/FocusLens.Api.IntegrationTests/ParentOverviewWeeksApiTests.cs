using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;
using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Api.IntegrationTests;

public sealed class ParentOverviewWeeksApiTests
{
    [Fact]
    public async Task SetOverviewWeek_WithStartsOn_PersistsHistoricalSelectionAndDashboardUsesIt()
    {
        DateTimeOffset now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        await using CustomWebApplicationFactory factory = new(now);
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        Student student = new(Guid.NewGuid());
        student.SetTimeZoneId("Africa/Cairo");
        student.SetParentSharingPreferences(true, true);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudentWeek historical = new(student.Id, new DateOnly(2026, 10, 3));

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new ApplicationUser { Id = parentUserId, FirstName = "Parent", LastName = "User" },
                new ApplicationUser { Id = student.UserId, FirstName = "Student", LastName = "User" });
            db.Parents.Add(parent);
            db.Students.Add(student);
            db.ParentStudentRelationships.Add(relationship);
            db.StudentWeeks.Add(historical);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateParentClient(factory, parentUserId);
        using StringContent content = new("""{"startsOn":"2026-10-03"}""", Encoding.UTF8, "application/json");

        HttpResponseMessage update = await client.PutAsync(
            $"/api/parents/students/{student.Id}/overview-week", content);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using JsonDocument updateDocument = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.Equal("2026-10-03", updateDocument.RootElement.GetProperty("startsOn").GetString());
        Assert.False(updateDocument.RootElement.GetProperty("isCurrentWeek").GetBoolean());

        HttpResponseMessage dashboard = await client.GetAsync($"/api/parents/students/{student.Id}/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        using JsonDocument dashboardDocument = JsonDocument.Parse(await dashboard.Content.ReadAsStringAsync());
        JsonElement week = dashboardDocument.RootElement.GetProperty("week");
        Assert.Equal("2026-10-03", week.GetProperty("startsOn").GetString());
        Assert.Equal("2026-10-09", week.GetProperty("endsOn").GetString());
        Assert.False(week.GetProperty("isCurrentWeek").GetBoolean());
    }

    [Fact]
    public async Task SetOverviewWeek_WithMisalignedStartsOn_ReturnsBadRequest()
    {
        await using CustomWebApplicationFactory factory = new(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        Student student = new(Guid.NewGuid());
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new ApplicationUser { Id = parentUserId, FirstName = "Parent", LastName = "User" },
                new ApplicationUser { Id = student.UserId, FirstName = "Student", LastName = "User" });
            db.Parents.Add(parent);
            db.Students.Add(student);
            db.ParentStudentRelationships.Add(relationship);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateParentClient(factory, parentUserId);
        using StringContent content = new("""{"startsOn":"2026-10-04"}""", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PutAsync(
            $"/api/parents/students/{student.Id}/overview-week", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"weekStart":"2026-10-03"}""")]
    [InlineData("""{}""")]
    [InlineData("""{"startOn":"2026-10-03"}""")]
    public async Task SetOverviewWeek_WithoutCanonicalStartsOn_ReturnsBadRequestAndDoesNotClearSelection(string body)
    {
        await using CustomWebApplicationFactory factory = new(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        Student student = new(Guid.NewGuid());
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        relationship.SetSelectedOverviewWeekStart(new DateOnly(2026, 10, 3));

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new ApplicationUser { Id = parentUserId, FirstName = "Parent", LastName = "User" },
                new ApplicationUser { Id = student.UserId, FirstName = "Student", LastName = "User" });
            db.Parents.Add(parent);
            db.Students.Add(student);
            db.ParentStudentRelationships.Add(relationship);
            return Task.CompletedTask;
        });

        using HttpClient client = CreateParentClient(factory, parentUserId);
        using StringContent content = new(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PutAsync(
            $"/api/parents/students/{student.Id}/overview-week", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ParentStudentRelationship persisted = await db.ParentStudentRelationships.SingleAsync(item => item.Id == relationship.Id);
        Assert.Equal(new DateOnly(2026, 10, 3), persisted.SelectedOverviewWeekStart);
    }

    private static HttpClient CreateParentClient(CustomWebApplicationFactory factory, Guid parentUserId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(parentUserId, "Parent"));
        return client;
    }
}
