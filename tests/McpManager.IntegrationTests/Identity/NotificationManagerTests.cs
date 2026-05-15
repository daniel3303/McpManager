using AwesomeAssertions;
using McpManager.Core.Data.Models.Notifications;
using McpManager.Core.Identity;
using McpManager.Core.Repositories.Identity;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Identity;

public class NotificationManagerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public NotificationManagerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Create_WithWarningTypeAndNullIcon_AssignsExclamationTriangleIcon()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var sut = sp.GetRequiredService<NotificationManager>();
        var admin = await sp.GetRequiredService<UserRepository>()
            .GetAll()
            .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);

        var notification = await sut.Create(
            admin,
            title: "warning",
            type: NotificationType.Warning
        );

        // The bell-dropdown frontend renders Icon as a Heroicon name; a
        // regression in the default-icon switch (e.g. dropping Warning ->
        // exclamation-triangle) would surface as a missing icon there with
        // the rest of the notification still working.
        notification.Icon.Should().Be("exclamation-triangle");
    }
}
