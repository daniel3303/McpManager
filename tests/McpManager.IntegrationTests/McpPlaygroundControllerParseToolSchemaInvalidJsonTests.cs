using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpPlaygroundControllerParseToolSchemaInvalidJsonTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpPlaygroundControllerParseToolSchemaInvalidJsonTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetToolForm_ToolWithUnparseableInputSchema_RendersFormWithoutFields()
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
            var server = await scope
                .ServiceProvider.GetRequiredService<McpServerManager>()
                .Create(
                    new McpServer
                    {
                        Name = $"pg-bad-{Guid.NewGuid():N}",
                        TransportType = McpTransportType.Http,
                        Uri = "https://upstream.invalid/mcp",
                    }
                );
            var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();
            tool = tools.Add(
                new McpTool
                {
                    Name = "broken",
                    McpServerId = server.Id,
                    InputSchema = "this is not valid json {",
                }
            );
            await tools.SaveChanges();
        }

        // ParseToolSchema's catch (JObject.Parse throws on the malformed
        // schema) was zero-hit — every other tool has valid JSON. The catch
        // must swallow it and return an empty-field dto so GetToolForm still
        // renders (200) instead of 500-ing the playground panel.
        var response = await client.GetAsync($"/McpPlayground/GetToolForm?toolId={tool.Id}", ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().NotContain("<select", "an unparsable schema yields no parsed fields");
    }
}
