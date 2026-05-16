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

public class McpImportExportManagerSingleServerObjectTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpImportExportManagerSingleServerObjectTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task Import_SingleServerObjectWithNameProperty_CreatesThatServer()
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

        // ParseServers' "single server object with a name property" branch
        // (not the Claude `mcpServers` wrapper, not an array) was zero-hit:
        // every Import test posts the wrapper form. A regression dropping this
        // branch would silently import zero servers from a bare object export.
        var name = $"single-{Guid.NewGuid():N}";
        var json = "{\"name\":\"" + name + "\",\"url\":\"https://upstream.invalid/mcp\"}";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token, ["json"] = json }
        );

        var response = await client.PostAsync("/McpServers/Import", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        (await repo.GetAll().AnyAsync(s => s.Name == name, ct))
            .Should()
            .BeTrue("the single-server-object branch must import that server");
    }
}
