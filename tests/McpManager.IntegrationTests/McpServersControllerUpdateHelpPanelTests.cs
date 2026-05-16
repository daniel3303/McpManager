using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerUpdateHelpPanelTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerUpdateHelpPanelTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostUpdateHelpPanel_HttpTransport_ReturnsHttpHelpPartial()
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

        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "any",
                ["TransportType"] = "Http",
            }
        );

        // UpdateHelpPanel (ModelState.Clear + PartialView "_HelpPanel") was
        // uncovered. Switching transport posts here and must re-render the
        // help partial bound to the posted TransportType (Http -> the HTTP
        // help text); a regression breaking the route/partial would 500 the
        // server form's help section.
        var response = await client.PostAsync("/McpServers/UpdateHelpPanel", form, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().Contain("Configure a remote HTTP MCP server");
    }
}
