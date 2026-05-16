using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerCreatePostCustomHeaderFilterTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerCreatePostCustomHeaderFilterTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostCreate_CustomHeadersWithBlankKey_DropsBlankAndPersistsNamedHeader()
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

        var name = $"hdr-{Guid.NewGuid():N}";
        // Every Create test omits CustomHeaders, so the blank-key filter
        // predicate on the Create path was zero-hit. Pins that a blank-key
        // header row is dropped while a named one survives — a regression
        // removing the Where would persist an empty header key into the
        // server's CustomHeaders dictionary.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = name,
                ["TransportType"] = "Http",
                ["Uri"] = "https://upstream.invalid/mcp",
                ["CustomHeaders[0].Key"] = "X-Keep",
                ["CustomHeaders[0].Value"] = "kept",
                ["CustomHeaders[1].Key"] = "",
                ["CustomHeaders[1].Value"] = "dropped",
            }
        );

        var response = await client.PostAsync("/McpServers/Create", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var server = await repo.GetAll().FirstAsync(s => s.Name == name, ct);
        server.CustomHeaders.Should().ContainKey("X-Keep");
        server.CustomHeaders.Should().NotContainKey("");
    }
}
