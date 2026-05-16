using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpImportExportManagerPerServerErrorTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpImportExportManagerPerServerErrorTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task Import_ServerThatFailsValidation_RecordsErrorAndContinues()
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

        var getResp = await client.GetAsync("/McpServers/Import", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // An empty config builds an HTTP server with no Uri, so
        // _serverManager.Create -> ValidateServer throws. The per-server
        // catch (Errors++ + message + continue) was zero-hit — every Import
        // test uses valid configs. A regression letting that throw escape the
        // loop would abort the whole import instead of skipping one bad server.
        var name = $"badimport-{Guid.NewGuid():N}";
        var json = "{\"mcpServers\":{\"" + name + "\":{}}}";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token, ["json"] = json }
        );

        var response = await client.PostAsync("/McpServers/Import", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Razor HTML-encodes the quotes around the name, so assert on the
        // stable substrings instead of the exact punctuation.
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("Error importing", "the per-server catch must record the failure");
        body.Should().Contain(name, "the failing server's name must appear in the error message");
    }
}
