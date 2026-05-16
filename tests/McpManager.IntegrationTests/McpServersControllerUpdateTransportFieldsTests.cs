using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerUpdateTransportFieldsTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerUpdateTransportFieldsTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostUpdateTransportFields_ReturnsTransportFieldsPartialBoundToPostedDto()
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

        var uri = $"https://transport-{Guid.NewGuid():N}.invalid/mcp";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "any",
                ["TransportType"] = "Http",
                ["Uri"] = uri,
            }
        );

        // UpdateTransportFields (ModelState.Clear + PartialView "_TransportFields")
        // was uncovered. Switching transport type in the server form posts here
        // and must re-render the partial bound to the posted dto; a regression
        // breaking the route/partial would 500 the form's dynamic section.
        var response = await client.PostAsync("/McpServers/UpdateTransportFields", form, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().Contain("name=\"Uri\"");
        html.Should().Contain(uri, "the partial must re-render bound to the posted Uri");
    }
}
