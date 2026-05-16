using AwesomeAssertions;
using McpManager.Core.Data.Models.Notifications;
using McpManager.Core.Identity;
using McpManager.Core.Repositories.Identity;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Identity;

public class NotificationManagerSystemIconTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public NotificationManagerSystemIconTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Create_WithSystemTypeAndNullIcon_AssignsCog6ToothIcon()
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
            title: "system notice",
            type: NotificationType.System
        );

        // The System arm of GetDefaultIcon was zero-hit (only Success/Warning
        // are exercised). A regression dropping `System -> cog-6-tooth` would
        // render the wrong/blank Heroicon for every system notification while
        // the rest of the entity still saves.
        notification.Icon.Should().Be("cog-6-tooth");
    }
}
