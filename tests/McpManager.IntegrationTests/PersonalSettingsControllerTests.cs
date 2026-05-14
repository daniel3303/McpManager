using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class PersonalSettingsControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public PersonalSettingsControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostToggleTheme_AsAuthenticatedAdmin_ReturnsOneOfTheKnownThemeNames()
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

        var response = await client.PostAsync("/PersonalSettings/ToggleTheme", content: null, ct);

        // Anonymous lowercase property `theme` round-trips through the
        // CamelCasePropertyNamesContractResolver unchanged. The two
        // accepted values are the DaisyUI theme names hard-coded in the
        // action — a regression that drops the theme map or breaks the
        // McpManager UserManager toggle would surface here as a 5xx or
        // a missing/empty theme field.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().MatchRegex("\"theme\":\\s*\"mcpmanager(-dark)?\"");
    }
}
