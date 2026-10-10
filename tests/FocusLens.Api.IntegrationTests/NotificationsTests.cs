using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Notifications;
using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Api.IntegrationTests;

public sealed class NotificationsTests
{
    [Fact]
    public async Task GetMine_All_ReturnsOnlyCurrentUsersNotificationsAndUnreadCount()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid currentUserId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        Notification unread = CreateNotification(currentUserId, NotificationCategory.StudyReminder);
        Notification read = CreateNotification(currentUserId, NotificationCategory.PrivacyInformationUpdated);
        Notification other = CreateNotification(otherUserId, NotificationCategory.SessionSummaryReady);
        read.MarkRead(DateTimeOffset.UtcNow);

        await SeedNotificationsAsync(factory, currentUserId, otherUserId, unread, read, other);

        using HttpClient client = CreateAuthenticatedClient(factory, currentUserId);
        JsonElement response = await client.GetFromJsonAsync<JsonElement>("/api/notifications?filter=All");

        Assert.Equal(2, response.GetProperty("notifications").GetArrayLength());
        Assert.Equal(1, response.GetProperty("unreadCount").GetInt32());
        Assert.DoesNotContain(response.GetProperty("notifications").EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == other.Id);
    }

    [Fact]
    public async Task GetMine_FiltersAlertsAndSystemCategories()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid userId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        await SeedNotificationsAsync(factory, userId, otherUserId,
            CreateNotification(userId, NotificationCategory.StudyGoalProposal),
            CreateNotification(userId, NotificationCategory.PrivacyInformationUpdated),
            CreateNotification(userId, NotificationCategory.System));

        using HttpClient client = CreateAuthenticatedClient(factory, userId);
        JsonElement alerts = await client.GetFromJsonAsync<JsonElement>("/api/notifications?filter=Alerts");
        JsonElement system = await client.GetFromJsonAsync<JsonElement>("/api/notifications?filter=System");

        Assert.Single(alerts.GetProperty("notifications").EnumerateArray());
        Assert.Equal("StudyGoalProposal", alerts.GetProperty("notifications")[0].GetProperty("category").GetString());
        Assert.Equal(2, system.GetProperty("notifications").GetArrayLength());
        Assert.All(system.GetProperty("notifications").EnumerateArray(), item =>
            Assert.Contains(item.GetProperty("category").GetString(),
                new[] { "PrivacyInformationUpdated", "System" }));
        Assert.Equal(3, alerts.GetProperty("unreadCount").GetInt32());
        Assert.Equal(3, system.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task MarkRead_OnlyChangesTheCurrentUsersNotification()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid currentUserId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        Notification own = CreateNotification(currentUserId, NotificationCategory.StudyReminder);
        Notification other = CreateNotification(otherUserId, NotificationCategory.StudyReminder);
        await SeedNotificationsAsync(factory, currentUserId, otherUserId, own, other);

        using HttpClient client = CreateAuthenticatedClient(factory, currentUserId);
        HttpResponseMessage ownResponse = await client.PostAsync($"/api/notifications/{own.Id}/read", null);
        HttpResponseMessage otherResponse = await client.PostAsync($"/api/notifications/{other.Id}/read", null);

        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull((await db.Notifications.FindAsync(own.Id))!.ReadAtUtc);
        Assert.Null((await db.Notifications.FindAsync(other.Id))!.ReadAtUtc);
    }

    [Fact]
    public async Task MarkAllRead_IsIdempotentAndDoesNotAffectAnotherUser()
    {
        await using CustomWebApplicationFactory factory = new();
        Guid currentUserId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        Notification first = CreateNotification(currentUserId, NotificationCategory.StudyReminder);
        Notification second = CreateNotification(currentUserId, NotificationCategory.System);
        Notification other = CreateNotification(otherUserId, NotificationCategory.System);
        await SeedNotificationsAsync(factory, currentUserId, otherUserId, first, second, other);

        using HttpClient client = CreateAuthenticatedClient(factory, currentUserId);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsync("/api/notifications/read-all", null)).StatusCode);

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.All(await db.Notifications.Where(item => item.RecipientUserId == currentUserId).ToListAsync(),
            item => Assert.NotNull(item.ReadAtUtc));
        Assert.Null((await db.Notifications.FindAsync(other.Id))!.ReadAtUtc);
    }

    private static HttpClient CreateAuthenticatedClient(CustomWebApplicationFactory factory, Guid userId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtTokenFactory.Create(userId, "Student"));
        return client;
    }

    private static Notification CreateNotification(Guid userId, NotificationCategory category) =>
        new(userId, NotificationAudience.Student, category, $"{category} title", $"{category} message");

    private static async Task SeedNotificationsAsync(
        CustomWebApplicationFactory factory,
        Guid currentUserId,
        Guid otherUserId,
        params Notification[] notifications)
    {
        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new ApplicationUser { Id = currentUserId, FirstName = "Current", LastName = "User" },
                new ApplicationUser { Id = otherUserId, FirstName = "Other", LastName = "User" });
            db.Notifications.AddRange(notifications);
            return Task.CompletedTask;
        });
    }
}