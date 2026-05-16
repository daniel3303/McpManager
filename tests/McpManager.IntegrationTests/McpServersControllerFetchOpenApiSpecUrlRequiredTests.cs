using System.Text.Json;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerFetchOpenApiSpecUrlRequiredTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerFetchOpenApiSpecUrlRequiredTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostFetchOpenApiSpec_WithBlankUrl_ReturnsUrlRequiredJson()
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

        // The blank-url guard (return Json{success:false,error:"URL is
        // required"}) was zero-hit — no test posts the "fetch spec from URL"
        // button with an empty url. A regression dropping it would hand "" to
        // HttpClient.GetStringAsync and surface a confusing exception instead.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token, ["url"] = "" }
        );

        var response = await client.PostAsync("/McpServers/FetchOpenApiSpec", form, ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Be("URL is required");
    }
}
