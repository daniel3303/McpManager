using System.Text.Json;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerPreviewOpenApiToolsParseFailureTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerPreviewOpenApiToolsParseFailureTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostPreviewOpenApiTools_WithUnparseableSpec_ReturnsJsonErrorNotThrow()
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

        // This JSON parses but has no paths, so OpenApiSpecParser.ParseSpec
        // throws InvalidOperationException. Only the catch (Exception) branch
        // turns that into Json{Success=false}; it was zero-hit (existing tests
        // pass valid specs). A regression letting the exception escape would
        // 500 the preview-tools button instead of showing the error inline.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["openApiSpecification"] = "{\"not\":\"an OpenAPI document\",\"paths\":null}",
            }
        );

        var response = await client.PostAsync("/McpServers/PreviewOpenApiTools", form, ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
