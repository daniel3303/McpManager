using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerEditToolGetSchemaArgumentsTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerEditToolGetSchemaArgumentsTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetEditTool_ToolWithSchemaProperties_RendersArgumentRowFromSchema()
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

        Guid serverId,
            toolId;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var server = await sp.GetRequiredService<McpServerManager>()
                .Create(
                    new McpServer
                    {
                        Name = $"et-schema-{Guid.NewGuid():N}",
                        TransportType = McpTransportType.Http,
                        Uri = "https://upstream.invalid/mcp",
                    }
                );
            var toolRepo = sp.GetRequiredService<McpToolRepository>();
            var tool = toolRepo.Add(
                new McpTool
                {
                    Name = "search",
                    McpServerId = server.Id,
                    InputSchema =
                        "{\"properties\":{\"query\":{\"description\":\"the search text\"}}}",
                }
            );
            await toolRepo.SaveChanges();
            serverId = server.Id;
            toolId = tool.Id;
        }

        // Every other EditTool test uses a not-found id or an empty schema, so
        // the schema-properties foreach (build McpToolArgumentDto per JSON
        // property) was zero-hit. Pins that a populated InputSchema surfaces
        // each argument name+description — a regression skipping the loop body
        // would silently drop per-argument customization fields from the form.
        var response = await client.GetAsync($"/McpServers/EditTool/{serverId}/{toolId}", ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("query");
        body.Should().Contain("the search text");
    }
}
