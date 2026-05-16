using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpPlaygroundControllerParseToolSchemaNoPropertiesTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpPlaygroundControllerParseToolSchemaNoPropertiesTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetToolForm_SchemaWithNoProperties_RendersFormWithoutFields()
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
                        Name = $"pg-np-{Guid.NewGuid():N}",
                        TransportType = McpTransportType.Http,
                        Uri = "https://upstream.invalid/mcp",
                    }
                );
            var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();
            // Valid JSON, parses fine, but has no "properties" object.
            tool = tools.Add(
                new McpTool
                {
                    Name = "noargs",
                    McpServerId = server.Id,
                    InputSchema = "{\"type\":\"object\"}",
                }
            );
            await tools.SaveChanges();
        }

        // ParseToolSchema parses the schema but `properties == null`, so it
        // returns the dto early with zero fields. That branch was zero-hit
        // (other tests use schemas with properties or invalid JSON). A
        // no-argument tool must still render its form (200), not 500 on the
        // null properties dereference.
        var response = await client.GetAsync($"/McpPlayground/GetToolForm?toolId={tool.Id}", ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().NotContain("<select", "a no-properties schema yields no parsed fields");
    }
}
