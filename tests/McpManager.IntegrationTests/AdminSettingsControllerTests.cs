using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class AdminSettingsControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AdminSettingsControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetIndex_AsSeededAdmin_RendersFormWithSeededAppSettings()
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

        var response = await client.GetAsync("/AdminSettings", ct);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);

        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        // ApplicationDbContext seeds AppSettings { Id = 1, McpConnectionTimeoutSeconds = 120,
        // McpRetryAttempts = 3 }. A migration that drops the seed row or renames the
        // columns would render empty inputs here even though the page still loads.
        document
            .QuerySelector("input[name='McpConnectionTimeoutSeconds']")!
            .GetAttribute("value")
            .Should()
            .Be("120");
        document
            .QuerySelector("input[name='McpRetryAttempts']")!
            .GetAttribute("value")
            .Should()
            .Be("3");
    }
}
