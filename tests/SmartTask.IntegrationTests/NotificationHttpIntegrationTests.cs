using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.Notifications;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;

namespace SmartTask.IntegrationTests;

public sealed class NotificationHttpIntegrationTests
{
    [SqlServerFact]
    public async Task NotificationEndpoints_EnforceRecipientIsolationAndReadLifecycle()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        var connectionBuilder =
            new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", connectionBuilder.InitialCatalog);

        await using var factory =
            new ProjectTestWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var userAId = Guid.NewGuid().ToString();
        var userBId = Guid.NewGuid().ToString();

        var userANotificationToReadId = Guid.NewGuid();
        var userARemainingNotificationId = Guid.NewGuid();
        var userBNotificationId = Guid.NewGuid();

        try
        {
            // Arrange: create two users with separate notifications.
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(
                    CreateUser(userAId),
                    CreateUser(userBId));

                context.Notifications.AddRange(
                    CreateNotification(
                        userANotificationToReadId,
                        userAId,
                        "TaskAssigned"),
                    CreateNotification(
                        userARemainingNotificationId,
                        userAId,
                        "TaskCommentAdded"),
                    CreateNotification(
                        userBNotificationId,
                        userBId,
                        "TaskStatusChanged"));

                await context.SaveChangesAsync();
            }

            // 1. User A sees only their own notifications.
            AuthenticateAs(client, userAId);

            var listResponse = await client.GetAsync(
                "/api/v1/notifications");

            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            var notificationList =
                await listResponse.Content
                    .ReadFromJsonAsync<NotificationListResponse>();

            Assert.NotNull(notificationList);
            Assert.Equal(2, notificationList.TotalCount);
            Assert.Equal(2, notificationList.UnreadCount);
            Assert.Equal(2, notificationList.Items.Count);
            Assert.All(
                notificationList.Items,
                item => Assert.Contains(
                    item.Id,
                    new[]
                    {
                        userANotificationToReadId,
                        userARemainingNotificationId
                    }));

            // 2. User A cannot mark User B's notification as read.
            var unauthorizedReadResponse = await client.PatchAsync(
                $"/api/v1/notifications/{userBNotificationId}/read",
                content: null);

            Assert.Equal(
                HttpStatusCode.NotFound,
                unauthorizedReadResponse.StatusCode);

            // 3. User A can mark their own notification as read.
            var markReadResponse = await client.PatchAsync(
                $"/api/v1/notifications/{userANotificationToReadId}/read",
                content: null);

            Assert.Equal(
                HttpStatusCode.NoContent,
                markReadResponse.StatusCode);

            // 4. Mark-all updates only User A's remaining unread notification.
            var markAllResponse = await client.PatchAsync(
                "/api/v1/notifications/read-all",
                content: null);

            Assert.Equal(HttpStatusCode.OK, markAllResponse.StatusCode);

            using var markAllJson =
                await markAllResponse.Content.ReadFromJsonAsync<JsonDocument>();

            Assert.NotNull(markAllJson);
            Assert.Equal(
                1,
                markAllJson.RootElement
                    .GetProperty("updatedCount")
                    .GetInt32());

            // 5. Verify persisted read states for both users.
            await using (var context = new SmartTaskDbContext(options))
            {
                var persistedNotifications =
                    await context.Notifications
                        .Where(notification =>
                            notification.RecipientUserId == userAId ||
                            notification.RecipientUserId == userBId)
                        .ToListAsync();

                var firstUserNotification =
                    Assert.Single(persistedNotifications,
                        notification =>
                            notification.Id == userANotificationToReadId);

                var secondUserNotification =
                    Assert.Single(persistedNotifications,
                        notification =>
                            notification.Id == userARemainingNotificationId);

                var otherUserNotification =
                    Assert.Single(persistedNotifications,
                        notification =>
                            notification.Id == userBNotificationId);

                Assert.True(firstUserNotification.IsRead);
                Assert.NotNull(firstUserNotification.ReadAtUtc);

                Assert.True(secondUserNotification.IsRead);
                Assert.NotNull(secondUserNotification.ReadAtUtc);

                Assert.False(otherUserNotification.IsRead);
                Assert.Null(otherUserNotification.ReadAtUtc);
            }
        }
        finally
        {
            await CleanupAsync(options, userAId, userBId);
        }
    }

    private static Notification CreateNotification(
        Guid id,
        string recipientUserId,
        string type)
    {
        return new Notification
        {
            Id = id,
            RecipientUserId = recipientUserId,
            Type = type,
            Title = "Integration test notification",
            Message = "Notification lifecycle integration test.",
            RelatedEntityType = "Task",
            IsRead = false
        };
    }

    private static ApplicationUser CreateUser(string userId)
    {
        var email = $"notification-test-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
    }

    private static void AuthenticateAs(HttpClient client, string userId)
    {
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
    }

    private static async Task CleanupAsync(
        DbContextOptions<SmartTaskDbContext> options,
        params string[] userIds)
    {
        await using var context = new SmartTaskDbContext(options);

        var notifications = await context.Notifications
            .Where(notification =>
                userIds.Contains(notification.RecipientUserId))
            .ToListAsync();

        context.Notifications.RemoveRange(notifications);

        var users = await context.Users
            .Where(user => userIds.Contains(user.Id))
            .ToListAsync();

        context.Users.RemoveRange(users);

        await context.SaveChangesAsync();
    }
}
