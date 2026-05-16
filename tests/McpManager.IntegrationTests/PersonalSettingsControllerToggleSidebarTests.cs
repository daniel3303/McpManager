using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class PersonalSettingsControllerToggleSidebarTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public PersonalSettingsControllerToggleSidebarTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostToggleSidebar_AsAuthenticatedAdmin_ReturnsCollapsedBoolJson()
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

        // ToggleSidebar was entirely zero-hit (only ToggleTheme was tested).
        // The sidebar toggle's persisted state drives the layout on every
        // page; a regression breaking the action or the lowercase `collapsed`
        // JSON shape would silently desync the sidebar from the user setting.
        var response = await client.PostAsync("/PersonalSettings/ToggleSidebar", content: null, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().MatchRegex("\"collapsed\":\\s*(true|false)");
    }
}
