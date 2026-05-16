using System.Text.Json;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerPreviewOpenApiToolsEmptySpecTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerPreviewOpenApiToolsEmptySpecTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostPreviewOpenApiTools_WithBlankSpec_ReturnsNoSpecificationJson()
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
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // The blank-spec guard (return Json{success:false,error:"No
        // specification provided"}) was zero-hit — the parse-failure test
        // (PR #240) hits the catch, not this early-out. A regression dropping
        // it would hand "" to OpenApiSpecParser and surface a confusing parser
        // error instead of a clear "provide a spec" message.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["openApiSpecification"] = "   ",
            }
        );

        var response = await client.PostAsync("/McpServers/PreviewOpenApiTools", form, ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Be("No specification provided");
    }
}
