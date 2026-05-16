using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerUpdateStdioModeTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerUpdateStdioModeTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostUpdateStdioMode_AdvancedMode_ReturnsStdioFieldsPartialWithCommand()
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

        var getResp = await client.GetAsync("/McpServers/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var createHtml = await getResp.Content.ReadAsStringAsync(ct);
        var formDoc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(createHtml), ct);
        var token = formDoc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        var command = $"cmd-{Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "any",
                ["TransportType"] = "Stdio",
                ["UseAdvancedCommand"] = "true",
                ["Command"] = command,
            }
        );

        // UpdateStdioMode (ModelState.Clear + PartialView "_StdioFields") was
        // uncovered. Toggling the stdio command mode posts here and must
        // re-render the partial bound to the posted dto (advanced branch shows
        // the Command field); a regression breaking the route/partial would
        // 500 the server form's stdio section.
        var response = await client.PostAsync("/McpServers/UpdateStdioMode", form, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().Contain("name=\"Command\"");
        html.Should().Contain(command, "the partial must re-render bound to the posted Command");
    }
}
