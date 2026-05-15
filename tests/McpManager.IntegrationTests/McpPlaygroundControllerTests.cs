using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpPlaygroundControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpPlaygroundControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetGetTools_WithUnknownServerId_Returns404()
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

        // GetTools returns NotFound() when the server lookup misses — the only
        // 404 path in this controller. A regression that returns 500 (unhandled
        // null) or 200 with an empty list would hide the missing-server case
        // from the playground UI's error toast.
        var response = await client.GetAsync($"/McpPlayground/GetTools/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGetToolForm_WithEnumAndNumericSchema_RendersParsedFields()
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

        McpTool tool;
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            var server = await manager.Create(
                new McpServer
                {
                    Name = $"pg-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
            var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();
            tool = tools.Add(
                new McpTool
                {
                    Name = "query",
                    McpServerId = server.Id,
                    InputSchema = """
                    {"type":"object","required":["mode"],"properties":{
                      "mode":{"type":"string","description":"the mode","enum":["fast","slow"]},
                      "count":{"type":"integer","description":"how many","default":3,"minimum":1,"maximum":10}}}
                    """,
                }
            );
            await tools.SaveChanges();
        }

        // GetToolForm -> ParseToolSchema was the whole 0%-covered hot path:
        // JObject parse, required JArray, properties loop, enum + numeric +
        // default + min/max branches. Asserting the rendered enum <select> and
        // numeric <input> pins that the schema parse maps both field shapes
        // (a regression in the enum/required handling renders the wrong control).
        var response = await client.GetAsync($"/McpPlayground/GetToolForm?toolId={tool.Id}", ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        var modeSelect = document.QuerySelector("select[name='mode']");
        modeSelect.Should().NotBeNull("the string+enum property must render as a <select>");
        modeSelect!
            .QuerySelectorAll("option")
            .Select(o => o.GetAttribute("value"))
            .Should()
            .Contain(["fast", "slow"]);
        document
            .QuerySelector("input[name='count'][type='number']")
            .Should()
            .NotBeNull("the integer property must render as a numeric input");
    }

    [Fact]
    public async Task GetGetTools_WithServerHavingTool_ReturnsToolJsonUsingDescriptionFallback()
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

        McpServer server;
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            server = await manager.Create(
                new McpServer
                {
                    Name = $"pg-tools-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
            var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();
            tools.Add(
                new McpTool
                {
                    Name = "search_docs",
                    Description = "fallback-desc",
                    CustomDescription = null,
                    McpServerId = server.Id,
                    InputSchema = "{}",
                }
            );
            await tools.SaveChanges();
        }

        // GetTools' found path (lines 57-72) was uncovered — only the 404 case
        // was. The projection uses `CustomDescription ?? Description`; with a
        // null custom desc the fallback must surface the original Description.
        // A regression flipping the coalesce would blank the playground list.
        var response = await client.GetAsync($"/McpPlayground/GetTools?serverId={server.Id}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("search_docs");
        body.Should().Contain("fallback-desc");
    }
}
