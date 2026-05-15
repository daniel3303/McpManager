using System.Net;
using AwesomeAssertions;
using McpManager.Core.Identity;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Notifications;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class NotificationsControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public NotificationsControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetUnreadCount_AsAuthenticatedAdmin_ReturnsCamelCaseCountJson()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;

        await _factory.SignInAsAdminAsync(client, ct);

        var response = await client.GetAsync("/Notifications/UnreadCount", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadAsStringAsync(ct);
        // Anonymous DTO is `new { Count = ... }` (PascalCase in C#).
        // CamelCasePropertyNamesContractResolver in Program.cs converts it
        // to "count" on the wire — a regression that drops the resolver
        // would silently return "Count" and break every frontend script
        // reading these JSON payloads.
        body.Should().Contain("\"count\"");
        body.Should().NotContain("\"Count\"");
    }

    [Fact]
    public async Task GetShow_WithUnreadNoUrlNotification_MarksReadAndRendersView()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        Guid notificationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var admin = await sp.GetRequiredService<UserRepository>()
                .GetAll()
                .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
            var created = await sp.GetRequiredService<NotificationManager>()
                .Create(admin, title: $"note-{Guid.NewGuid():N}", message: "hello");
            notificationId = created.Id;
        }

        // Only UnreadCount was covered. Show's found+unread+no-url path runs the
        // not-found bypass, the MarkAsRead branch, the empty-Url skip, and the
        // View render. Asserting the persisted IsRead pins the side effect — a
        // regression that drops MarkAsRead leaves the bell badge stuck.
        var response = await client.GetAsync($"/Notifications/Show/{notificationId}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<NotificationRepository>();
        var reloaded = await repo.Get(notificationId);
        reloaded!.IsRead.Should().BeTrue("Show must mark an unread notification as read");
    }
}
