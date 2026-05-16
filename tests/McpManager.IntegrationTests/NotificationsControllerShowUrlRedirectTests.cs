using System.Net;
using AwesomeAssertions;
using McpManager.Core.Identity;
using McpManager.Core.Repositories.Identity;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class NotificationsControllerShowUrlRedirectTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public NotificationsControllerShowUrlRedirectTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetShow_NotificationWithUrl_RedirectsToThatUrl()
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
                .Create(
                    admin,
                    title: $"note-{Guid.NewGuid():N}",
                    message: "deep link",
                    url: "/McpServers"
                );
            notificationId = created.Id;
        }

        // The existing Show test seeds a no-Url notification, so the
        // `!string.IsNullOrWhiteSpace(notification.Url) -> Redirect(Url)`
        // branch was zero-hit. Clicking a notification that carries a deep
        // link must 302 to it; a regression dropping that branch would render
        // the details view instead of navigating the user where they expect.
        var response = await client.GetAsync($"/Notifications/Show/{notificationId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/McpServers");
    }
}
