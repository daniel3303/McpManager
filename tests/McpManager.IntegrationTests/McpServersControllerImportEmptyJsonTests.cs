using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerImportEmptyJsonTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerImportEmptyJsonTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostImport_WithBlankJson_ReRendersWithValidationError()
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

        // The blank-json guard (ModelState error + return View(), before the
        // ImportExportManager is touched) was zero-hit: the existing Import
        // test always posts valid JSON. A regression dropping the guard would
        // hand an empty string to the importer instead of re-prompting.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token, ["json"] = "   " }
        );

        var response = await client.PostAsync("/McpServers/Import", form, ct);

        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "blank import input must re-render the form, not redirect");
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("name=\"json\"", "the Import form must be re-rendered");
        // The guard returns View() WITHOUT an ImportResult; if it were dropped,
        // a blank string would reach the importer and an import-result summary
        // (the success/skipped/errors badges) would render instead.
        body.Should().NotContain("badge badge-success", "the importer must not have run");
    }
}
