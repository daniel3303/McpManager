using AwesomeAssertions;
using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Identity;
using McpManager.Core.Repositories.Identity;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Identity;

public class UserManagerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UserManagerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task ToggleTheme_OnLightUser_FlipsUserToDarkAndReturnsDark()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var sut = sp.GetRequiredService<UserManager>();
        var users = sp.GetRequiredService<UserRepository>();

        var user = await users.GetAll().FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
        user.Theme = Theme.Light;
        await users.SaveChanges();

        var returned = await sut.ToggleTheme(user);

        // PersonalSettingsControllerTests covers the HTTP path but asserts on
        // either of two valid strings — the toggle direction itself
        // (Light -> Dark, not Light -> Light) is only pinned here.
        returned.Should().Be(Theme.Dark);
        user.Theme.Should().Be(Theme.Dark);
    }
}
